using System.Text;
using System.Windows.Forms;

namespace HydraServer;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // Protege qualquer acesso a Console (WinForms pode n�o ter console)
        try
        {
            // S� tenta ajustar se houver um handle v�lido
            // Alguns ambientes (WinExe) n�o exp�em stdout e isso lan�a IOException.
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
        catch (System.IO.IOException)
        {
            // Sem console dispon�vel � ignora
        }
        catch (UnauthorizedAccessException)
        {
            // Sem permiss�o para mexer no stream � ignora
        }
        catch
        {
            // Qualquer outro problema, tamb�m ignoramos para n�o quebrar a UI
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }
}
