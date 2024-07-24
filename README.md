# USTS
## UDP String Transmission System 
### Based on the UDP network protocol, using C# and Python programming languages, with support for MongoDB and the ability to work in Docker containers.
  
  
#### Used dependencies:
##### C#:
######   **!Need for installation!** NuGet:
    MongoDB.Driver
    MongoDB.Bson
######   In code:
    using System; 
    using System.Net.Sockets; 
    using System.Net; 
    using System.Text; 
    using System.Threading.Tasks; 
    using System.Collections.Concurrent; 
    using System.IO; 
    using System.Diagnostics; 
    using System.Text.Json;
    using MongoDB.Driver; 
    using MongoDB.Bson; 

##### Python:
######    In Code:
    import socket 
    import uuid 
    import time 
    
