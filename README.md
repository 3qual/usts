# USTS
## UDP String Transmission System 
### Based on UDP network protocol, C# and Python Programming Languages and ability to work under Docker Containers 
  
  
#### Dependencies:
##### C#:
######   NuGet:
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
    
