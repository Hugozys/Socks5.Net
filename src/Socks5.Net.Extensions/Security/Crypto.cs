using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;
using NSec.Experimental;
using Socks5.Net.Extensions.Security;

namespace Socks5.Net.Security
{
    public static class Crypto
    {
        private class KeyExchange{}
        private static readonly Lazy<ILogger<KeyExchange>> _logger = new Lazy<ILogger<KeyExchange>>(() => Socks.LoggerFactory.CreateLogger<KeyExchange>());
        public const int ECDHPubKeySize = 32;
        public const int SaltSize = 32;

        public const int NonceFixedFieldSize = 8;
        public const int NonceCntSize = 4;

        public const int NonceSize = NonceFixedFieldSize + NonceCntSize;

        public const int Chacha20StreamBlockSize = 64;

        public static Task<Stream> GetServerStreamAsync(NetworkStream originalStream, Mode mode) => mode switch
        {
            Mode.PlainText => Task.FromResult<Stream>(originalStream),
            Mode.Xor => GetServerRandomXORStreamAsync(originalStream),
            _ => GetServerCryptoStreamAsync(originalStream)
        };

        /* Protocol
        * client generates a key pair. It will be used for client's every connection
        * For every connection, client sends 80 bytes to server
        * - salt: 32 bytes
        * - nonce1: 8 bytes
        * - nonce2: 8 bytes
        * - client pub key: 32 bytes
        * 
        * server generates a key pair. It will be used for server's every connection
        * For every connection, server sends 32 bytes to client
        * - server pub key: 32 bytes
        *
        * shared_secret = X25519(client_priv, server_public) = X25519(server_priv, client_public)
        *
        * In Xor mode
        * seed1 = HKDFSha256(shared_secret, salt, nonce1, 4B)  (RFC-5869)
        * seed2 = HKDFSha256(shared_secret, salt, nonce2, 4B)
        * client->server:
        * - client encrypts: output = Xor(input, Random(seed1))
        * - server decrypts: output = Xor(input, Random(seed1))
        * server->client:
        * - server encrypts: output = Xor(input, Random(seed2))
        * - client decrypts: output = Xor(input, Random(seed2))
        *
        * In Chacha20 mode
        * shared_key = HKDFSha256(shared_secret, salt)
        * client->server: stream operates on (shared_key, nonce1)
        * server->client: stream operates on (shared_key, nonce2)
        */
        public static Task<Stream> GetClientStreamAsync(NetworkStream originalStream, Mode mode) => mode switch
        {
            Mode.PlainText => Task.FromResult<Stream>(originalStream),
            Mode.Xor => GetClientRandomXORStreamAsync(originalStream),
            _ => GetClientCryptoStreamAsync(originalStream)
        };

        public static async Task<Stream> GetServerCryptoStreamAsync(NetworkStream originalStream)
        {
            var sharedCtx = await ServerKeyExchangeAsync(originalStream);

            var chachaKey = KeyDerivationAlgorithm.HkdfSha256.DeriveKey(
                sharedCtx.Secret, 
                sharedCtx.Salt, 
                Span<byte>.Empty,
                StreamCipherAlgorithm.ChaCha20, new KeyCreationParameters{ ExportPolicy = KeyExportPolicies.AllowPlaintextArchiving});
            return new Chacha20Stream(originalStream, chachaKey, sharedCtx.IngressNonce, sharedCtx.EgressNonce);
        }

        public static async Task<Stream> GetClientCryptoStreamAsync(NetworkStream originalStream)
        {   
            var sharedCtx = await ClientKeyExchangeAsync(originalStream);
            var chachaKey = KeyDerivationAlgorithm.HkdfSha256.DeriveKey(
                sharedCtx.Secret, 
                sharedCtx.Salt, 
                Span<byte>.Empty,
                StreamCipherAlgorithm.ChaCha20);
            return new Chacha20Stream(originalStream, chachaKey, sharedCtx.IngressNonce, sharedCtx.EgressNonce);
        }

        public static async Task<Stream> GetClientRandomXORStreamAsync(NetworkStream originalStream)
        {
            var sharedCtx = await ClientKeyExchangeAsync(originalStream);
            
            var (ingressSeed, egressSeed) = GetSeedFromSharedCtx(sharedCtx);

            return new RandomXORStream(originalStream, ingressSeed, egressSeed);
        }

        public static async Task<Stream> GetServerRandomXORStreamAsync(NetworkStream originalStream)
        {   
            var sharedCtx = await ServerKeyExchangeAsync(originalStream);
            var (ingressSeed, egressSeed) = GetSeedFromSharedCtx(sharedCtx);
            return new RandomXORStream(originalStream, ingressSeed, egressSeed);
        }

        private static (int ingressSeed, int egressSeed) GetSeedFromSharedCtx(SharedCtx sharedCtx)
        {
            var egressSeedBytes = KeyDerivationAlgorithm.HkdfSha256.DeriveBytes(
                sharedCtx.Secret, 
                sharedCtx.Salt, 
                sharedCtx.EgressNonce,
                sizeof(int));

            var ingressSeedBytes = KeyDerivationAlgorithm.HkdfSha256.DeriveBytes(
                sharedCtx.Secret, 
                sharedCtx.Salt, 
                sharedCtx.IngressNonce,
                sizeof(int));
            var egressSeed = BitConverter.ToInt32(egressSeedBytes);
            var ingressSeed = BitConverter.ToInt32(ingressSeedBytes);
            _logger.Value.LogInformation($"egressSeed: {egressSeed}, ingressSeed: {ingressSeed}");
            return (ingressSeed, egressSeed);
        }

        private static async Task<SharedCtx> ClientKeyExchangeAsync(NetworkStream originalStream)
        {
            var clientKey = Key.Create(KeyAgreementAlgorithm.X25519);
            Debug.Assert(clientKey.Size == ECDHPubKeySize, $"Public Key Size SHOULD Be {ECDHPubKeySize}, But Found {clientKey.PublicKey.Size}");
            var clientPubKeyBytes = clientKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);
            var randomBytes = new byte[SaltSize + NonceFixedFieldSize*2];
            RandomGen.Value.GetBytes(randomBytes);            
            var sentBuffer = new List<byte>();
            sentBuffer.AddRange(randomBytes);
            sentBuffer.AddRange(clientPubKeyBytes);
            ReadOnlyMemory<byte> exchangeBytes = sentBuffer.ToArray();
            var saltBytes = exchangeBytes[..SaltSize].ToArray();
            var ingressNonceBytes = exchangeBytes.Slice(SaltSize, NonceFixedFieldSize).ToArray();
            var egressNonceBytes = exchangeBytes.Slice(SaltSize + NonceFixedFieldSize, NonceFixedFieldSize).ToArray();
            _logger.Value.LogDebug($@"
            salt bytes: {string.Join(',',saltBytes)}
            ingress nonce bytes: {string.Join(',', ingressNonceBytes)}
            egress nonce bytes bytes: {string.Join(',',egressNonceBytes)}
            client pub key bytes: {string.Join(',', clientPubKeyBytes)}");
            _logger.Value.LogInformation("Sending exchange info to server...");
            await originalStream.WriteAsync(exchangeBytes, default);

            _logger.Value.LogInformation("Reading server exchange info...");
            Memory<byte> buffer = new byte[ECDHPubKeySize];
            var readBuffer = buffer;
            int remaining = buffer.Length;
            while( remaining > 0 )
            {
                int readBytes = await originalStream.ReadAsync(readBuffer);
                if (readBytes == 0)
                {
                    throw new Exception($"Expected to receive {buffer.Length} for key exchange, but only got {buffer.Length - remaining} bytes");
                }
                readBuffer = readBuffer[readBytes..];
                remaining -= readBytes;
            }
            var serverPubKeyBytes = buffer.ToArray();
            _logger.Value.LogDebug($"Read bytes: {string.Join(',', serverPubKeyBytes)}");
            var serverPublicKey = PublicKey.Import(KeyAgreementAlgorithm.X25519, serverPubKeyBytes, KeyBlobFormat.RawPublicKey);
            var sharedSecret = KeyAgreementAlgorithm.X25519.Agree(clientKey, serverPublicKey) ?? throw new ArgumentException("Derived Null Shared Secret");
            return new (sharedSecret, saltBytes, ingressNonceBytes, egressNonceBytes);
        }

        private static async Task<SharedCtx> ServerKeyExchangeAsync(NetworkStream originalStream)
        {
            var serverKey = Key.Create(KeyAgreementAlgorithm.X25519);
            Debug.Assert(serverKey.Size == ECDHPubKeySize, $"Public Key Size SHOULD Be {ECDHPubKeySize}, But Found {serverKey.PublicKey.Size}");

            Memory<byte> buffer = new byte[SaltSize + NonceFixedFieldSize*2 + ECDHPubKeySize ];
            var readBuffer = buffer;
            _logger.Value.LogInformation("Reading Client Payload...");
            int remaining = buffer.Length;
            while(remaining > 0)
            {
                int readBytes = await originalStream.ReadAsync(readBuffer);
                if (readBytes == 0)
                {
                    throw new Exception($"Expected to receive {buffer.Length} for key exchange, but only got {buffer.Length - remaining} bytes");
                }
                readBuffer = readBuffer[readBytes..];
                remaining -= readBytes;
            }
            var saltBytes = buffer[..SaltSize].ToArray();
            var ingressNonceBytes = buffer.Slice(SaltSize, NonceFixedFieldSize).ToArray();
            var egressNonceBytes = buffer.Slice(SaltSize + NonceFixedFieldSize, NonceFixedFieldSize).ToArray();
            var clientPubKeyBytes = buffer.Slice(SaltSize + 2*NonceFixedFieldSize, ECDHPubKeySize).ToArray();
            _logger.Value.LogInformation($@"
            salt bytes: {string.Join(',',saltBytes)}
            IngressNonce bytes: {string.Join(',', ingressNonceBytes)}
            EgressNonce bytes: {string.Join(',',egressNonceBytes)}
            client pub key bytes: {string.Join(',', clientPubKeyBytes)}");

            var serverPubKeyBytes = serverKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);
            _logger.Value.LogInformation($"Sending server public key: {string.Join(',',serverPubKeyBytes)}");
            await originalStream.WriteAsync(serverPubKeyBytes);

            var clientPublicKey = PublicKey.Import(KeyAgreementAlgorithm.X25519, clientPubKeyBytes, KeyBlobFormat.RawPublicKey);
            var sharedSecret = KeyAgreementAlgorithm.X25519.Agree(serverKey, clientPublicKey) ?? throw new ArgumentException("Derived Null Shared Secret");
            return new (sharedSecret, saltBytes, egressNonceBytes, ingressNonceBytes);
        }

        private readonly struct SharedCtx
        {
            public SharedSecret Secret { get; }

            public byte[] Salt { get; }

            public byte[] IngressNonce { get; }

            public byte[] EgressNonce { get; }
            public SharedCtx(SharedSecret secret, byte[] salt, byte[] ingressNonce, byte[] egressNonce)
            {
                Secret = secret;
                Salt = salt;
                IngressNonce = ingressNonce;
                EgressNonce = egressNonce;
            }
        }
        internal static readonly Lazy<RandomNumberGenerator> RandomGen = new Lazy<RandomNumberGenerator>(() => RandomNumberGenerator.Create());

    }
}