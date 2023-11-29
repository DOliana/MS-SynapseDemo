using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace synapse_funcs
{
    public class ProcessFilesParameters
    {
        public string SourceFilepath { get; set; }
        public string SourceContainerName { get; set; }
        public string TargetContainerName { get; set; }
        public string TargetFolderPath { get; set; }
    }
}
