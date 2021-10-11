#nullable disable

namespace Kinetix.Tools.Model.FileModel
{
    public class Alias
    {
        public string File { get; set; }

        public string[] Classes { get; set; } = new string[0];

        public string[] Endpoints { get; set; } = new string[0];
    }
}
