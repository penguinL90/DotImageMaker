namespace ConsoleApp1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            int k = 0;
            Parallel.For(0, 12800, i =>
            {
                for (int j = 0; j < 12800; ++j)
                {
                    ++k;
                }
            });
            Console.WriteLine(k);
            Console.ReadKey();
        }
    }
}
