namespace Kinetix.Tools.Model.FileModel
{
    public struct FileName
    {
        public string Module { get; set; }

        public string Kind { get; set; }

        public string File { get; set; }

        public override string ToString()
        {
            return $"{Module}/{Kind}/{File}";
        }
    }
}
