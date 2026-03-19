using System;
using System.Threading.Tasks;
using GuildDialogue.Services;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. 인코딩 프로바이더 등록 (CP949 등 지원)
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        // 2. 터미널 출력만 UTF-8로 설정 (입력은 터미널 기본값 유지)
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var manager = new DialogueManager();
        await manager.InitializeAsync();

        Console.WriteLine("실행 모드를 선택하세요:");
        Console.WriteLine("1. 동료 간 관전 모드 (자동 대화)");
        Console.WriteLine("2. 길드장 직접 참여 모드 (사용자 입력)");
        Console.Write("> ");
        
        var choice = Console.ReadLine();

        if (choice == "2")
        {
            await manager.RunGuildMasterSessionAsync();
        }
        else
        {
            await manager.RunInteractiveSessionAsync();
        }
    }
}
