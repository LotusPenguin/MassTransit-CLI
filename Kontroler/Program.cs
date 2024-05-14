using MassTransit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wiadomosci;
using MassTransit.Serialization;

namespace Kontroler
{
    class ControllerRequest : IControllerRequest
    {
        public bool dziala { get; set; }
        public ControllerRequest(bool dziala)
        {
            this.dziala = dziala;
        }
    }

    class Klucz : SymmetricKey
    {
        public byte[] Key { get; set; }

        public byte[] IV { get; set; }
    }

    public class Dostawca : ISymmetricKeyProvider
    {
        private string k;
        public Dostawca(string k) { this.k = k; }
        public bool TryGetKey(string id, out SymmetricKey key)
        {
            var sk = new Klucz();
            sk.IV = Encoding.ASCII.GetBytes(id.Substring(0,16));
            sk.Key = Encoding.ASCII.GetBytes(k);
            key = sk;
            Console.Out.WriteLineAsync();
            Console.Out.WriteLineAsync($"IV: {id.Substring(0, 16)}");
            return true;
        }
    }

    internal class Program
    {
        static ISendEndpoint sendEP = null;
        static void DisplayMenu()
        {
            Console.WriteLine("Controller initialized");
            Console.WriteLine("s - start the genenerator");
            Console.WriteLine("t - stop the generator");
            Console.WriteLine("ESC - quit");
        }

        static void Ustaw(bool dziala)
        {
            sendEP.Send<Wiadomosci.IControllerRequest>(new ControllerRequest(dziala), ctx =>
            {
                ctx.Headers.Set("timestamp", DateTime.Now.ToString());
                ctx.Headers.Set(EncryptedMessageSerializer.EncryptionKeyHeader, Guid.NewGuid().ToString());
            });

            Console.Out.WriteLineAsync();
            if (dziala)
            {
                Console.WriteLine("Generator start request sent.");
            }
            else
            {
                Console.WriteLine("Generator stop request sent.");
            }
        }

        static void Main(string[] args)
        {
            var exitFlag = false;

            var bus = Bus.Factory.CreateUsingRabbitMq(sbc =>
            {
                sbc.Host(new Uri("rabbitmq://cow.rmq2.cloudamqp.com/xgjwajpd"), h =>
                {
                    h.Username("xgjwajpd");
                    h.Password("gMGsMovgDYfZHxL1F7ca2sjkY_zhWKiN");
                });
                sbc.UseEncryptedSerializer(
                    new AesCryptoStreamProvider(
                        new Dostawca("18466318466318466318466318466318"),
                            "0123456789abcdef"));
            });
            bus.Start();

            var tsk = bus.GetSendEndpoint(new Uri("rabbitmq://cow.rmq2.cloudamqp.com/xgjwajpd/controllerqueue"));
            tsk.Wait();
            sendEP = tsk.Result;

            DisplayMenu();

            while(!exitFlag)
            {
                var input = Console.ReadKey().Key;

                switch(input)
                {
                    case ConsoleKey.S:
                        Ustaw(true);
                        break;

                    case ConsoleKey.T:
                        Ustaw(false);
                        break;

                    case ConsoleKey.Escape:
                        exitFlag = true;
                        break;

                    default:
                        break;
                }
            }

            bus.Stop();
        }
    }
}
