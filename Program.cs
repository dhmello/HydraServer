using System.Text;
using System.Windows.Forms;

namespace HydraServer;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // Protege qualquer acesso a Console (WinForms pode não ter console)
        try
        {
            // Só tenta ajustar se houver um handle válido
            // Alguns ambientes (WinExe) não expõem stdout e isso lança IOException.
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
        catch (System.IO.IOException)
        {
            // Sem console disponível — ignora
        }
        catch (UnauthorizedAccessException)
        {
            // Sem permissão para mexer no stream — ignora
        }
        catch
        {
            // Qualquer outro problema, também ignoramos para não quebrar a UI
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }
}
