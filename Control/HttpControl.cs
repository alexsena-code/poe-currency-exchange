using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CurrencyExchange.Commands;

namespace CurrencyExchange.Control;

/// <summary>
/// Canal de controle LOCAL via HTTP (127.0.0.1). Recebe comandos de fora (curl/CLI/agente da VPS),
/// ENFILEIRA no runner e awaita o resultado (completado pela render thread). Roda numa thread de fundo;
/// só toca a fila (thread-safe) — nunca a memória do jogo. Bind em 127.0.0.1 não exige urlacl/admin.
///
/// Rotas:
///   POST /cmd     {"name":"ping","args":[]}     -> {ok,message,data,id}
///   GET  /commands                              -> {commands:[...]}
///   GET  /logs?n=50                             -> texto (tail do LogBus)
/// </summary>
public sealed class HttpControl : IDisposable
{
    private readonly int _port;
    private readonly CommandRunner _runner;
    private readonly CommandRegistry _reg;
    private readonly LogBus _log;
    private readonly Action<string> _diag;
    private HttpListener _listener;
    private Thread _thread;
    private volatile bool _running;

    private const int CmdTimeoutMs = 30000;

    public HttpControl(int port, CommandRunner runner, CommandRegistry reg, LogBus log, Action<string> diag)
    { _port = port; _runner = runner; _reg = reg; _log = log; _diag = diag; }

    public bool Running => _running;

    public void Start()
    {
        if (_running) return;
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "cx-http" };
            _thread.Start();
            _diag?.Invoke($"[http] ouvindo em http://127.0.0.1:{_port}/");
        }
        catch (Exception e) { _diag?.Invoke($"[http] falhou ao iniciar na porta {_port}: {e.Message}"); }
    }

    private void Loop()
    {
        while (_running)
        {
            HttpListenerContext ctx;
            try { ctx = _listener.GetContext(); }
            catch { break; }   // listener parado
            Task.Run(() => { try { Handle(ctx); } catch { } });
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath?.TrimEnd('/') ?? "";
        var method = ctx.Request.HttpMethod;

        if (method == "GET" && path == "/commands")
        { WriteJson(ctx, 200, new { commands = _reg.Names.ToArray() }); return; }

        if (method == "GET" && path == "/logs")
        {
            int n = int.TryParse(ctx.Request.QueryString["n"], out var v) ? v : 50;
            WriteText(ctx, 200, _log.Tail(n)); return;
        }

        if (method == "POST" && path == "/cmd")
        {
            string body;
            using (var r = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding ?? Encoding.UTF8))
                body = r.ReadToEnd();
            string name; string[] args;
            try
            {
                var o = JObject.Parse(body);
                name = (string)o["name"];
                args = (o["args"] as JArray)?.Select(t => (string)t).ToArray() ?? Array.Empty<string>();
            }
            catch (Exception e) { WriteJson(ctx, 400, new { ok = false, message = $"JSON inválido: {e.Message}" }); return; }

            var tcs = new TaskCompletionSource<CommandResult>();
            var req = new CommandRequest { Name = name, Args = args, Tcs = tcs };
            _runner.Enqueue(req);

            if (tcs.Task.Wait(CmdTimeoutMs))
            {
                var res = tcs.Task.Result;
                WriteJson(ctx, res.Ok ? 200 : 422, new { ok = res.Ok, message = res.Message, data = res.Data, id = req.Id });
            }
            else
                WriteJson(ctx, 504, new { ok = false, message = "timeout — o plugin está avançando comandos? (em jogo + Enable ligado?)", id = req.Id });
            return;
        }

        WriteJson(ctx, 404, new { ok = false, message = $"rota desconhecida: {method} {path}" });
    }

    private static void WriteJson(HttpListenerContext ctx, int status, object payload)
        => Write(ctx, status, "application/json", JsonConvert.SerializeObject(payload));

    private static void WriteText(HttpListenerContext ctx, int status, string text)
        => Write(ctx, status, "text/plain; charset=utf-8", text);

    private static void Write(HttpListenerContext ctx, int status, string contentType, string content)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(content ?? "");
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = contentType;
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }
        catch { }
        finally { try { ctx.Response.Close(); } catch { } }
    }

    public void Dispose()
    {
        _running = false;
        try { _listener?.Stop(); } catch { }
        try { _listener?.Close(); } catch { }
    }
}
