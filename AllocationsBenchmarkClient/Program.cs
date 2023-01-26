using System.Net;

const int Runs = 3;
const int Iterations = 10_000;

// cd C:\MihaZupan\reverse-proxy\testassets\TestServer
// C:\MihaZupan\reverse-proxy\.dotnet\dotnet run --urls "http://localhost:10010;http://localhost:10011"

// cd C:\MihaZupan\reverse-proxy\AllocationsBenchmarkClient
// C:\MihaZupan\reverse-proxy\.dotnet\dotnet run

var uri = new Uri("https://localhost:5001");

while (true)
{
    Console.WriteLine("----------------");
    Console.WriteLine("Waiting to start");
    Console.ReadLine();

    try
    {
        using var client = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            UseCookies = false,
            UseProxy = false,
        })
        {
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        for (var run = 1; run <= Runs; run++)
        {
            if (run != 0)
            {
                Console.WriteLine("Sleeping");
                await Task.Delay(5000);
            }

            for (var i = 0; i < Iterations; i++)
            {
                await client.GetStringAsync(uri);

                if (i % 100 == 0)
                {
                    Console.WriteLine($"Run {run} iteration {i}");
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        Console.WriteLine();
    }
}
