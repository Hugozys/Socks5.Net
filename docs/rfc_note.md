# BIND
1. client establishes a TCP connection with proxy server
2. client sends a BIND cmd to proxy server along with the ip address and port number of target host
3. proxy server creates and binds a new socket and listen for connection
4. proxy server sends back a reply message with the ip address and port assosiated with the socket
5. client sends the ip address and port which the proxy server is listening on to application server
6. app server tries to connect to the proxy server with the ip address and port
7. proxy server accepts and **ONLY** the connection coming from the specified ip address and port.
8. Once the connection is established, proxy server sends back 2nd reply to client with the connected host's ip address and port

# UDP ASSOCIATE
1. client establishes a TCP connection with proxy server
2. client sends a UDP ASSOSIATE cmd to proxy server along with the ip address and port number it will use to send the UDP Diagram
3. proxy server creates a UDP bind and listen for packets
4. proxy server sends a reply to client with the ip address and port number to which the client must send UDP datagram
5. client starts blasting UDP packets with specified header attached at the beginning
6. proxy server drops any UDP packet that's not coming from the specified source
7. proxy server sends the UDP packet to destination read from the header
8. proxy server receivese the response UDP packet and sends back to the client
9. proxy server ends the UDP connection if the assosiated TCP connection is closed


 