using UnityEngine;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;

public class TwitchIRC : MonoBehaviour{
    public static bool isOnline;

    private const string clientId = "sftfosuv5fkksmyr7sv3cl3zz1ivbl";
    private const string oauth = "oauth:tuj9zfpd5oohdhv5737ecxgqp34p9g";
    private const string nickName = "twitchchatlurker";
    private const string server = "irc.chat.twitch.tv";
    private const int port = 6667;

    private static TwitchChatExample _manager;

    public string channelName;
    public bool channelIsLive;
    
    public class MsgEvent : UnityEngine.Events.UnityEvent<string,string> { }
    public MsgEvent messageRecievedEvent = new MsgEvent();

    private Thread threadConnect;
    System.Net.Sockets.TcpClient sock = null;
    private string buffer = string.Empty;
    private Queue<string> commandQueue = new Queue<string>();
    private List<string> recievedMsgs = new List<string>();
    private Socket client;

    private string channelId;
    private string playingGame = "";

    private readonly List<int> lengthClipHtml = new List<int>{37,38,39,42};
    private int _htmlStreamType = 12;
    private int _htmlGame = 21;

    private float lastTime;
    private float _lastTimeLive;
    private float _lastTimeOffline;
    private float _lastTimePlaying;
    private float _timeOutConnection;

    [HideInInspector]public List<float> timeMsg = new List<float>();

    private void StartIRC(){
        if(!isOnline){
            StopAllCoroutines();
            StartCoroutine("InternetReach");
            return;
        }

        lastTime = Time.time;
        _timeOutConnection = Time.time;
        timeMsg.Add(lastTime);

        channelId = "";
        _lastTimeLive = -120;
        _timeOutConnection = Time.time;
        StartCoroutine("ConnectToChatIRC");
    }

    private IEnumerator IRCInputProcedure(System.IO.TextReader input, System.Net.Sockets.NetworkStream networkStream)
    {
        while (true){
            yield return new WaitForEndOfFrame();

            if(!isOnline){

                RemoveChatActivity();
                StopAllCoroutines();
                StartCoroutine("InternetReach");
                yield break;
            }
            else if (!networkStream.DataAvailable || !channelIsLive){
                continue;
            }

            buffer = input.ReadLine();

            if (buffer.Contains("PRIVMSG #")){
                if (recievedMsgs != null){
                    recievedMsgs.Add(buffer);
                    timeMsg.Add(Time.time-lastTime);
                    lastTime = Time.time;
                    _timeOutConnection = Time.time;
                    float _handleTime = 0;
                    foreach (float item in timeMsg){
                        _handleTime += item;
                    }
                    _manager.RefreshChatActivity(new Activity(){name = channelName, time = _handleTime/timeMsg.Count, isLive = channelIsLive});
                }
            }

            if (buffer.StartsWith("PING ")){
                SendCommand(buffer.Replace("PING", "PONG"));
            }

            if (buffer.Split(' ')[1].Contains("001")){
                SendCommand("JOIN #" + channelName);
            }
        }
    }

    private IEnumerator IRCOutputProcedure(System.IO.TextWriter output)
    {
        while (true){
            yield return new WaitForEndOfFrame();
            if (commandQueue != null){
                if (commandQueue.Count > 0){
                    output.WriteLine(commandQueue.Peek());
                    output.Flush();
                    commandQueue.Dequeue();
                }
            }
        }
    }

    private IEnumerator InternetReach(){
        yield return new WaitUntil(() => isOnline);
        StartIRC();
    }

    private IEnumerator ConnectToChatIRC(){
        StartCoroutine("ChannelStatus");
        yield return new WaitUntil(() => channelIsLive);
        bool flag = false;
        sock = new TcpClient();
        new Thread(new ThreadStart(delegate{
            sock.Connect(server, port);
            while(!sock.Connected){
                return;
            }
            flag = true;
            Thread.CurrentThread.Abort();
        })).Start();

        yield return new WaitUntil(()=> flag);
        var networkStream = sock.GetStream();
        var input = new System.IO.StreamReader(networkStream);
        var output = new System.IO.StreamWriter(networkStream);

        output.WriteLine("PASS " + oauth);
        output.WriteLine("NICK " + nickName.ToLower());
        output.Flush();

        StartCoroutine(IRCOutputProcedure(output));
        StartCoroutine(IRCInputProcedure(input, networkStream));
    }

    public IEnumerator ChannelStatus(){
        yield return new WaitUntil(() => channelName != "");
        WWW _request;
        if(channelId == ""){
            _request = new WWW("https://api.twitch.tv/kraken/users?login="+channelName+"&client_id="+clientId+"&api_version=5");
            yield return _request;
            if(_request.error != null){
                yield break;
            }
            if(_request.text != ""){
                foreach (string item in _request.text.Split(',')){
                    if(item.Contains("\"_id\"")){
                        channelId = item.Split(':')[1].Split('"')[1];
                        break;
                    }
                }
            }
        }
        if(channelId != ""){
            _request = new WWW("https://api.twitch.tv/kraken/streams/?channel="+channelId+"&client_id="+clientId+"&api_version=5");
            yield return _request;

            if(_request.text != ""){
                if(_request.text.Split(',').Length > 3){
                    bool hThread = false;
                    string hHtml = _request.text;
                    new Thread(new ThreadStart(delegate{
                        List<bool> flags = new List<bool>{false,false};
                        for (int i = 0; i < hHtml.Split(',').Length; i++){
                            if(hHtml.Split(',')[i].Contains("\"stream_type\"")){
                                _htmlStreamType = i;
                                flags[0] = true;
                            }
                            else if(hHtml.Split(',')[i].Contains("\"game\"")){
                                _htmlGame = i;
                                flags[1] = true;
                            }

                            if(!flags.Contains(false)){
                                break;
                            }
                        }

                        hThread = true;
                        Thread.CurrentThread.Abort();
                    })).Start();
                    yield return new WaitUntil(() => hThread);

                    bool isLiveNow = false;
                    if(_request.text.Split(',')[_htmlStreamType].Split(':')[1].Contains("\"live\"") && _lastTimeOffline < Time.time){
                        _lastTimeLive = Time.time;
                        if(!channelIsLive){
                            channelIsLive = true;
                            playingGame = "";
                            isLiveNow = true;
                            _manager._liveSound.Play();
                            Windows.FlashWindow();
                            _lastTimePlaying = -120;
                            _timeOutConnection = Time.time;
                        }
                    }

                    string gameName = "";
                    for (int i = 1; i < _request.text.Split(',')[_htmlGame].Split(':').Length; i++){
                        if(i > 1){
                            gameName += ":";
                        }
                        gameName += _request.text.Split(',')[_htmlGame].Split(':')[i];
                    }

                    if(playingGame.Contains(gameName)){
                        _lastTimePlaying = Time.time;
                    }
                    else if(!playingGame.Contains(gameName) && channelIsLive){
                        if((Time.time - _lastTimePlaying) > 120){
                            playingGame = gameName;
                            if(isLiveNow){
                                _manager.OnDebugLog("<color=lime><size=10>"+System.DateTime.Now.Hour.ToString("0") + ":" + System.DateTime.Now.Minute.ToString("00") +"</size> / "+ channelName + " is live playing "+playingGame+".</color>");
                            }
                            else{
                                _manager.OnDebugLog("<size=10>"+System.DateTime.Now.Hour.ToString("0") + ":" + System.DateTime.Now.Minute.ToString("00") +"</size> / "+ channelName + " is playing "+playingGame);
                            }
                        }
                    }
                }
                else if(channelIsLive){
                    if((Time.time - _lastTimeLive) > 120){
                        _manager.OnDebugLog("<color=#FF7F00FF><size=10>"+System.DateTime.Now.Hour.ToString("0") + ":" + System.DateTime.Now.Minute.ToString("00") +"</size> / "+ channelName + " is done.</color>");
                        _lastTimeOffline = Time.time + 300;
                        channelIsLive = false;
                        playingGame = "";
                        RemoveChatActivity();
                        StopAllCoroutines();
                        StartIRC();
                    }
                }
            }
        }
        if(channelIsLive){
            if(Time.time - _timeOutConnection > 60f){
                if(sock != null){
                    sock.Close();
                }
                channelId = "";
                RemoveChatActivity();
                StopAllCoroutines();
                StartIRC();
            }
        }
        yield return new WaitForSeconds(60f);
        StopCoroutine("ChannelStatus");
        StartCoroutine("ChannelStatus");
    }

    public void PlayingGame(){
        if(channelIsLive && playingGame != ""){
            _manager.OnDebugLog(channelName + " is playing "+playingGame);
        }
    }

    public void RemoveChatActivity(){
        if(sock != null){
            sock.Close();
        }
        _manager.RefreshChatActivity(new Activity(){name = channelName, time = 300f, isLive = false});
    }

    public void SendCommand(string cmd){
        if (commandQueue != null){
            commandQueue.Enqueue(cmd);
        }
    }

    public void SendMsg(string msg){
        if (commandQueue != null){
            commandQueue.Enqueue("PRIVMSG #" + channelName + " :" + msg);
        }
    }

    void OnEnable()
    {
        if(_manager == null){
            _manager = GetComponent<TwitchChatExample>();
        }
        StartIRC();
    }

    void OnDisable()
    {
        if(sock != null){
            sock.Close();
        }
        channelId = "";
        channelIsLive = false;
        RemoveChatActivity();
        StopAllCoroutines();
        StartIRC();
    }

    void OnDestroy()
    {
        channelId = "";
        channelIsLive = false;
        RemoveChatActivity();
        StopAllCoroutines();
    }

    private void OnApplicationQuit()
    {
        if(sock != null){
            sock.Close();
        }
    }

    void Update(){

        if(timeMsg.Count > 20){
            timeMsg.RemoveAt(0);
        }
        if (recievedMsgs != null)
        {
            if (recievedMsgs.Count > 0)
            {
                for (int i = 0; i < recievedMsgs.Count; i++)
                {
                    messageRecievedEvent.Invoke(recievedMsgs[i],channelName);
                }
                recievedMsgs.Clear();
            }
        }
    }
}