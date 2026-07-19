namespace MusiQL.Etl;

public static class EtlLog
{
    public static void Write(string message) =>
        Console.WriteLine($"{DateTime.Now:HH:mm:ss} {message}");
}
