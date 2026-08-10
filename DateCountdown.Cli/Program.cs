using DateCountdown.Cli;
using System;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

return await new CountdownCliApplication(Console.Out, Console.Error).RunAsync(args);
