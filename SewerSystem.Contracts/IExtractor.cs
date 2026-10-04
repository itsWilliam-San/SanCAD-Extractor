using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ComponentModel;

namespace SewerSystem.Contracts
{
    public class TextEntity
    {
        public struct Position
        {
            public double X {get;set;} 
            public double Y {get;set;}
        }

        private string _content;
        
        public string Content
        {
            get => _content;
            set => _content = value?.Trim() ?? string.Empty;
        }

        public Position Coordinates { get; set; }
    }
    
    public interface IExtractor
    {
        public Manhole Entry(TextEntity[] textEntity); 
    }
}