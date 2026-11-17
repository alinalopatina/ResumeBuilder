using Xunit;

namespace ResumeBuilder.Tests;

/// <summary>
/// Общая подготовка для всего тестового класса (выполняется ОДИН раз на класс):
/// создаёт временную папку перед первым тестом и удаляет её после последнего.
/// Подключается к классу через IClassFixture&lt;TestFolderFixture&gt;.
/// </summary>
public class TestFolderFixture : IDisposable
{
    public string Folder { get; }

    public TestFolderFixture()
    {
        Folder = Path.Combine(Path.GetTempPath(), "ResumeBuilderTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
    }

    public void Dispose()
    {
        if (Directory.Exists(Folder))
            Directory.Delete(Folder, recursive: true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Тесты, которые меняют общий язык сообщений (Messages.Culture), собраны в одну «коллекцию»,
/// чтобы xUnit не запускал их параллельно и они не мешали друг другу.
/// </summary>
[CollectionDefinition("Messages")]
public class MessagesCollection
{
}
