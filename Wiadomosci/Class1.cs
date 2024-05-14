using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wiadomosci
{
    public interface IPubl
    {
        string tekst { get; set; }
    }
    public interface IOdpA
    {
        string kto { get; set; }
    }
    public interface IOdpB
    {
        string kto { get; set; }
    }
    public interface IControllerRequest
    {
        bool dziala { get; set; }
    }
}
