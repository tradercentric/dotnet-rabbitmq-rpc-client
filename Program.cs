using System;
using System.Threading.Tasks;

namespace RabbitMQRpcClient
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            using var rpcClient = new RpcClient();
            var task = Task.Run(() => rpcClient.Call("dir"));
            var isCompletedSuccessfully = task.Wait(TimeSpan.FromMilliseconds(10000));
            if (!isCompletedSuccessfully)
            {
                throw new TimeoutException("The function has taken longer than the maximum time allowed.");
            }

            var response = task.Result;
            Console.WriteLine($"{response}");
        }
    }
}