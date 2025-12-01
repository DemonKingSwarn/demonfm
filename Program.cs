using demonfm.filemanager;

public class Program
{
  static void Main(string[] args)
  {
    var fileManager = new FileManager();
    fileManager.Initialize();

    while(FileManager.running)
    {
      try
      {
        fileManager.Draw();
        fileManager.HandleInput();
      }
      catch  (Exception ex)
      {
        fileManager.DisplayError(ex.Message);
      }
    }

    Console.ResetColor();
    Console.Clear();
    Console.CursorVisible = true;
  }
}
