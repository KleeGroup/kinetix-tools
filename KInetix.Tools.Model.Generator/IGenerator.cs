using Kinetix.Tools.Model.FileModel;

namespace Kinetix.Tools.Model.Generator
{
    public interface IGenerator
    {
        bool CanGenerate { get; }
        string Name { get; }
        void GenerateAll();
        void GenerateFromFile(ModelFile file);
    }
} 
