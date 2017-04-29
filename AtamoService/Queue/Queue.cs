using System;
using System.Collections.Generic;

namespace Atamo
{
    public class AtamoQueue<T>
    {
        private readonly ILogger<AtamoQueue<T>> _logger;
        Dictionary<int, IAtamoDestination<T>> destinations;
        IAtamoSource<T> source;


    }
}