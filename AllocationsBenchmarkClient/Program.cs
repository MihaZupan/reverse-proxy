const int Runs = 3;
const int Iterations = 10_000;

var uri = new Uri("http://localhost:5000");

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
            UseProxy = false
        });

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
