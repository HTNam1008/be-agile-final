namespace Moe.StudentFinance.E2EHost;

public class Program
{
    public static async System.Threading.Tasks.Task Main(string[] args)
    {
        // Automatically inject the E2EHostingStartup mocks into the API pipeline
        System.Environment.SetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "Moe.StudentFinance.E2EHost");

        // Execute the exact product code from the API host
        var apiMethod = typeof(global::Program).GetMethod("<Main>$", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (apiMethod != null)
        {
            var result = apiMethod.Invoke(null, new object[] { args });
            if (result is System.Threading.Tasks.Task task)
            {
                await task;
            }
        }
        else
        {
            System.Console.WriteLine("Could not find <Main>$ in global::Program");
        }
    }
}
