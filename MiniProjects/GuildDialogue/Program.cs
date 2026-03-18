using System;
using System.Threading.Tasks;
using GuildDialogue.Services;

namespace GuildDialogue;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var manager = new DialogueManager();
            await manager.InitializeAsync();
            await manager.RunInteractiveSessionAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Fatal Error] {ex.Message}\n{ex.StackTrace}");
        }
    }
}
