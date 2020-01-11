namespace Kinetix.Tools.Model.Generator
{
    public interface IGenerator
    {
        string Name { get; }

        bool CanGenerate { get; }
        void Generate();
    }
}
