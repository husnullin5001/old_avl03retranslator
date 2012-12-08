﻿using System;
using System.Globalization;
using System.Threading;
using TcpServer.Core;

namespace TcpServer.TestBasePacket
{
    class Program
    {
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-Us");

            var packet = string.Empty;

            packet = "$$A5359772032415375|AAUA1146.41953N030.28760E030056|01.5|00.8|01.2|20120322135053|20120322135053|000011000000|14152725|00000000|2D52C7C1|0000|0.9689|0833||00033|6CDB";
            //packet = "$$A5359772032415375|AAUA1146.42979N030.30972E025056|01.5|00.8|01.2|20120322135300|20120322135300|000011000000|14152725|00000000|00085939|0000|0.9812|0835||00042|CCEE";
            //packet = "$$9F359772035621383|91UA1155.71857N037.51489E000000|01.7|00.8|01.5|20120120094502|20120120094502|000000000000|14151196|00000000|1E487BC8|0000|0.0000|0524||56C3";

            var basePacket = BasePacket.GetFromGlonass(packet);


            //22.03.12 17:51:59 
            //src: $$A5359772032415375|AAUA1146.41953N030.28760E030056|01.5|00.8|01.2|20120322135053|20120322135053|000011000000|14152725|00000000|2D52C7C1|0000|0.9689|0833||00033|6CDB
            //dst: $$B7359772032415375|AA$GPRMC,175159.814,A,4625.1719,N,3017.2559,E,30.00,56.00,220312,0,N,A*26|1.5|0.8|1.2|000011000000|20120322135022|14152725|00000000|2D52C7C1|0000|0.9689|0833||884F

            //22.03.12 17:52:56 
            //src: $$A5359772032415375|AAUA1146.42979N030.30972E025056|01.5|00.8|01.2|20120322135300|20120322135300|000011000000|14152725|00000000|00085939|0000|0.9812|0835||00042|CCEE
            //dst: $$B7359772032415375|AA$GPRMC,175256.785,A,4625.7876,N,3018.5833,E,25.00,56.00,220312,0,N,A*20|1.5|0.8|1.2|000011000000|20120322135322|14152725|00000000|00085939|0000|0.9812|0835||6820

            //24.03.12 09:15:48 
            //src: $$A5359772032423460|AAUA1146.30998N030.06716E020134|01.6|00.8|01.4|20120324051536|20120324051535|000001000000|14192802|00000341|000802DC|0000|0.4318|0629||00000|0EDD
            //dst: $$B8359772032423460|AA$GPRMC,091548.633,A,4618.5986,N,3004.0295,E,20.00,134.00,240312,0,N,A*15|1.6|0.8|1.4|000001000000|20120324051524|14192802|00000341|000802DC|0000|0.4318|0629||3718
            
            
            Console.Read();
        }

        private static string GetPppPacket(string advPacket)
        {
            var ppp = new PPP();
            var packet = ppp.Make_PPP_Packet(advPacket);
            packet = ppp.PackData(packet);
            return packet;
        }
    }
}