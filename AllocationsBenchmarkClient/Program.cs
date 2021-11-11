using System.Net.WebSockets;
using System.Text;

const int Runs = 10;
const int Iterations = 1000;

// cd C:\MihaZupan\reverse-proxy\testassets\TestServer
// C:\MihaZupan\reverse-proxy\.dotnet\dotnet run --urls "http://localhost:10010;http://localhost:10011"

// cd C:\MihaZupan\reverse-proxy\AllocationsBenchmarkClient
// C:\MihaZupan\reverse-proxy\.dotnet\dotnet run

var uri = new Uri("http://localhost:5000");

var webSockets = new List<ClientWebSocket>(Iterations * Runs);

for (var run = 1; run <= Runs; run++)
{
    Console.WriteLine("----------------");
    Console.WriteLine("Waiting to start");
    Console.ReadLine();

    try
    {
        //using (var client = new HttpClient())
        //{
        //    for (var i = 0; i < 100; i++)
        //    {
        //        await client.GetStringAsync("http://localhost:5000/get");
        //    }
        //}

        var _helloWorld = Encoding.UTF8.GetBytes("Hello world!");
        var _receiveBuffer = new byte[8192];

        for (var i = 1; i <= Iterations; i++)
        {
            var webSocket = new ClientWebSocket();
            webSocket.Options.RemoteCertificateValidationCallback = delegate { return true; };
            webSockets.Add(webSocket);

            await webSocket.ConnectAsync(new Uri("ws://localhost:5000/websocket"), CancellationToken.None);

            await webSocket.SendAsync(_helloWorld, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

            while (!(await webSocket.ReceiveAsync(_receiveBuffer, CancellationToken.None)).EndOfMessage) { }

            if (i % 100 == 0)
            {
                Console.WriteLine($"Connected {i} WebSockets");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        Console.WriteLine();
    }
}
