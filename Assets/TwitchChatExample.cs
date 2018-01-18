using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

public struct Activity{
    public float time;
    public string name;
    public bool isLive;
}

public struct ClipInfo{
    public string id;
    public string time;
}

public class TwitchChatExample : MonoBehaviour
{
    #region Constant Fields

    public static readonly string[] ignoreUrl = { "https://clips.twitch.tv/create"};

    #endregion

    #region Public Fields

    [Header("Lists")]
    public CanvasGroup _filterGroup;
    public CanvasGroup _channelGroup;
    public CanvasGroup _chatActGroup;

    [Header("Chat")]
    public RectTransform _parent;
    public GameObject _sample;
    public GameObject _urlSample;

    [Header("User Filter Assets")]
    public Text _filterUsers;
    public Toggle _activeFilterToggle;
    public InputField _userInputField;
    public Text _inputAction;

    [Header("Channel Chat Assets")]
    public Text _channelList;
    public InputField _channelInputField;
    public Text _inputActionChannel;

    [Header("Audio Feedback")]
    public AudioSource _msgSound;
    public AudioSource _urlSound;
    public AudioSource _liveSound;

    [Header("Url Filter Assets")]
    public Toggle _activeUrlFilter;

    [Header("Context Menu")]
    public RectTransform _contextMenu;

    [Header("Debug")]
    public Text _statusConnection;
    public Text _logConnection;

    #endregion

    #region Private Fields

    private string[] _userColors = {
        "#FF0000FF",
        "#FF4500FF",
        "#FF7F50FF",
        "#FF69B4FF",
        "#B22222FF",
        "#008000FF",
        "#2E8B57FF",
        "#5F9EA0FF",
        "#9ACD32FF",
        "#00FF7FFF",
        "#DAA520FF",
        "#D2691EFF",
        "#1E90FFFF",
        "#8A2BE2FF"
    };

    private List<Activity> _chatActivity = new List<Activity>();
    private List<TwitchIRC> _channelsChat;
    private List<ClipInfo> _dontRepeatLink = new List<ClipInfo>();
    private List<string> users;
    private List<string> channels;
    private Button[] _btnsContextMenu;
    private Vector3 _mousePosition;
    private RectTransform _chatActivityRect;
    private bool _activeFilter;
    private bool _activeUrl;
    private bool _filterFlag;
    private bool removeUser;
    private bool removeChannel;
    private string persistentDataPath;

    #endregion

    private void OnChatMsgReceived(string msg, string channelName){
        string msgTime = "";
        int msgIndex = msg.IndexOf("PRIVMSG #");
        string msgString = msg.Substring(msgIndex + channelName.Length + 11);
        string user = msg.Substring(1, msg.IndexOf('!') - 1);
        MatchCollection mc = Regex.Matches(msgString, @"(www[^ \s]+|http[^ \s]+)([\s]|$)", RegexOptions.IgnoreCase);
        _filterFlag = false;
        if(_activeFilter){
            int _colorSet = 0;
            foreach (string item in users){
                if(user.ToString() == item.ToString()){
                    user = "<color="+_userColors[_colorSet % (_userColors.Length)]+">" + user + "</color>";
                    _filterFlag = true;
                    _msgSound.Play();
                    Windows.FlashWindow();
                    msgTime = DateTime.Now.Hour.ToString("0") + ":" + DateTime.Now.Minute.ToString("00");
                }
                _colorSet++;
            }
        }

        if (_activeUrl){
            if(mc.Count > 0){
                if(mc[0].Value.Contains("https://clips.twitch.tv/")){
                    _filterFlag = true;
                    msgTime = DateTime.Now.Hour.ToString("0") + ":" + DateTime.Now.Minute.ToString("00");
                }
            }
        }

        if(msgString.Contains("twitchchatlurker")){
            _filterFlag = true;
        }

        if(!_activeFilter || _filterFlag){
            if (mc.Count > 0){
                GameObject _urlInstance = Instantiate(_urlSample,_parent)as GameObject;
                _urlInstance.transform.SetSiblingIndex(0);
                Text _text = _urlInstance.GetComponent<Text>();
                _text.text = "<size=10>"+msgTime+" "+channelName.ToUpper()+"</size>" + "/"+ user + ": " + msgString+"";;
                _urlInstance.GetComponent<Button>().onClick.AddListener(delegate{
                    _text.color = Color.magenta;
                    Application.OpenURL(mc[0].Value);
                });
                if(mc[0].Value.Contains("clips.twitch.tv")){
                    StartCoroutine(CheckLink(mc[0].Value,_urlInstance));
                }
            }
            else{
                Text _instance = Instantiate(_sample,_parent).GetComponent<Text>();
                _instance.transform.SetSiblingIndex(0);
                _instance.text = "<size=10>"+msgTime+" "+channelName.ToUpper()+"</size>" + "/"+ user + ": " + msgString+"";
            }
        }
    }

    public void OnDebugLog(string _value){
        Text _instance = Instantiate(_sample,_parent).GetComponent<Text>();
        _instance.transform.SetSiblingIndex(0);
        _instance.text = "<size=15><color=grey>> "+_value+"</color></size>";
    }

    private void Start()
    {
        StartCoroutine("CheckInternet");
        _contextMenu.position = Vector2.zero;
        _btnsContextMenu = _contextMenu.GetComponentsInChildren<Button>();
        foreach (Button item in _btnsContextMenu){
            item.interactable = false;
        }
        _statusConnection.text = "<color=red>OFFLINE</color>";
        TwitchIRC.isOnline = false;
        persistentDataPath = Application.persistentDataPath;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        if(_chatActivityRect == null){
            _chatActivityRect = _logConnection.GetComponent<RectTransform>();
        }

        _logConnection.text = "";

        _filterGroup.alpha = 0.1f;
        _channelGroup.alpha = 0.1f;

        _userInputField.text = "";
        _userInputField.onValueChanged.AddListener(delegate{
            if (_userInputField.text == ""){
                _inputAction.text = "";
            }
            else if (_userInputField.text[0] == '!'){
                removeUser = false;
                _inputAction.text = "!backup  \n!clear  \n!opendata  \n!refresh  \n!removeall  \n!removelast  \n!savelinks  \n!urlcount  \n\nCommand";
            }
            else if(char.IsLetterOrDigit(_userInputField.text[0])){
                _userInputField.text = Regex.Replace(_userInputField.text, @"[^A-Za-z0-9_\~]+", "");
                _userInputField.text = Regex.Replace(_userInputField.text, "~", "");
                removeUser = false;
                foreach (string item in users){
                    if (item == _userInputField.text){
                        removeUser = true;
                    }
                }
                if(removeUser){
                    _inputAction.text = "Remove";
                }
                else{
                    _inputAction.text = "Add";
                }
            }
            else{
                _userInputField.text = "";
            }

        });
        _userInputField.onEndEdit.AddListener(delegate{
            if(removeUser){
                OnRemoveUser();
            }
            else{
                OnAddUser();
            }
        });

        _channelInputField.text = "";
        _channelInputField.onValueChanged.AddListener(delegate{
            if (_channelInputField.text == ""){
                _inputActionChannel.text = "";
            }
            else if (_channelInputField.text[0] == '!'){
                _inputActionChannel.text = "!backup\n!clear\n!leaveall\n!leavelast\n!playing\n!refresh\n\nCommand";
                removeChannel = false;
            }
            else if (char.IsLetterOrDigit(_channelInputField.text[0])){
                
                _channelInputField.text = Regex.Replace(_channelInputField.text, @"[^A-Za-z0-9_\~]+", "");
                _channelInputField.text = Regex.Replace(_channelInputField.text, "~", "");
                removeChannel = false;
                foreach (string item in channels){
                    if(item == _channelInputField.text){
                        removeChannel = true;
                    }
                }
                if(removeChannel){
                    _inputActionChannel.text = "Leave";
                }
                else{
                    _inputActionChannel.text = "Join";
                }
            }
            else{
                _channelInputField.text = "";
            }
        });
        _channelInputField.onEndEdit.AddListener(delegate{
            if(removeChannel){
                OnRemoveChannel();
            }
            else{
                OnAddChannel();
            }
        });

        _inputAction.text = "";
        _inputActionChannel.text = "";


        if(PlayerPrefs.GetInt("activeFilterToggle") == 0){
            _activeFilter = false;
        }
        else{
            _activeFilter = true;
        }

        _filterUsers.text = "";
        users = new List<string>();
        if (PlayerPrefs.GetString("activeFilter") != ""){
            foreach (string item in PlayerPrefs.GetString("activeFilter").Split('|')){
                users.Add(item);
                if(_filterUsers.text.Length > 0){
                    _filterUsers.text += "\n" + item;
                }
                else{
                    _filterUsers.text = item;
                }
            }
        }

        _activeUrl = true;

        _activeFilterToggle.isOn = _activeFilter;
        _activeUrlFilter.isOn = _activeUrl;

        _channelList.text = "";
        channels = new List<string>();
        _channelsChat = new List<TwitchIRC>();
        if (PlayerPrefs.GetString("channels") != ""){
            foreach (string item in PlayerPrefs.GetString("channels").Split('|')){
                channels.Add(item);
                if(_channelList.text.Length > 0){
                    _channelList.text += "\n" + item.ToLower();
                }
                else{
                    _channelList.text += item.ToLower();
                }
                
            }
            StartCoroutine("QueueChatJoin");
        }

        new Thread(new ThreadStart(delegate {
            if (System.IO.File.Exists(persistentDataPath+"/filteredUrls.txt")) {
                System.IO.FileStream fs = new System.IO.FileStream(persistentDataPath+"/filteredUrls.txt", System.IO.FileMode.Open);
                using (System.IO.StreamReader sr = new System.IO.StreamReader(fs)){
                    foreach (string item in sr.ReadToEnd().Split('\n')){
                        if(item != ""){
                            _dontRepeatLink.Add(new ClipInfo(){id = item.Split('|')[0], time = item.Split('|')[1]});
                        }
                    }
                    sr.Close();
                }
                fs.Close();
            }

            Thread.CurrentThread.Abort();
        })).Start();
    }

    private void Update(){
        if (Application.isFocused){
            if(Input.GetMouseButtonDown(1)){
                _contextMenu.localPosition = new Vector2(Input.mousePosition.x-(Screen.width/2),Input.mousePosition.y-(Screen.height/2));
                foreach (Button item in _btnsContextMenu){
                item.interactable = true;
            }
            }
            else if (Input.GetMouseButtonUp(0)){
                _contextMenu.position = Vector2.zero;
                foreach (Button item in _btnsContextMenu){
                    item.interactable = false;
                }
            }
        }
    }

    private void FixedUpdate(){
        if(Application.isFocused){
            if(_inputAction.text != "" || _inputActionChannel.text != ""){
                if(_inputAction.text != ""){
                    _filterGroup.alpha = 1f;
                }
                else{
                    _channelGroup.alpha = 1f;
                }
            }
            else if(Input.mousePosition.y < 190f && Input.mousePosition.y > 0){
                if(Screen.width-Input.mousePosition.x < 300 && Screen.width-Input.mousePosition.x > 0){
                    _filterGroup.alpha = 1f;
                }
                else{
                    _filterGroup.alpha = 0.1f;
                }

                if(Input.mousePosition.x < 200 && Input.mousePosition.x > 0){
                    _channelGroup.alpha = 1f;
                }
                else{
                    _channelGroup.alpha = 0.1f;
                }
            }
            else{
                _filterGroup.alpha = 0.1f;
                _channelGroup.alpha = 0.1f;
            }

            if(Input.mousePosition.y > Screen.height-_chatActivityRect.rect.height-30 && Input.mousePosition.x > Screen.width-_chatActivityRect.rect.width-50 && Input.mousePosition.y < Screen.height && Input.mousePosition.x < Screen.width){
                if(_chatActGroup.alpha != 1f){
                    _chatActGroup.alpha = 1f;
                }
            }
            else{
                if(_chatActGroup.alpha != 0.1f){
                    _chatActGroup.alpha = 0.1f;
                }
            }
        }
    }

    private IEnumerator CheckInternet(){
        float delayTime = Time.time;
        WWW check = new WWW("https://api.twitch.tv/kraken/base");
        yield return new WaitUntil(() => ((Time.time - delayTime) > 5f) || check.isDone);

        if(!check.isDone && ((Time.time - delayTime) > 5f)){
            if(TwitchIRC.isOnline){
                _statusConnection.text = "<color=red>OFFLINE</color>";
                TwitchIRC.isOnline = false;
                OnDebugLog("<size=10>"+DateTime.Now.Hour.ToString("0") + ":" + DateTime.Now.Minute.ToString("00")+"</size> / Lost connection.");
            }
        }
        else if(check.text != ""){
            if(!TwitchIRC.isOnline){
                _statusConnection.text = "<color=lime>ONLINE</color>";
                TwitchIRC.isOnline = true;
            }
        }
        yield return new WaitForSeconds(60f);
        StartCoroutine("CheckInternet");
    }

    private void OnApplicationQuit(){
        if(_dontRepeatLink.Count > 0){
            SaveLinks();
        }
    }

    public void OnToggleChanged(){
        _activeFilter = _activeFilterToggle.isOn;
        OnDebugLog("FILTER USER " + (_activeFilterToggle.isOn?"ON":"OFF"));

        PlayerPrefs.SetInt("activeFilterToggle",_activeFilter? 1:0);
    }

    public void OnUrlToggleChanged(){
        _activeUrl = _activeUrlFilter.isOn;
        OnDebugLog("FILTER TWITCH CLIPS " + (_activeUrlFilter.isOn?"ON":"OFF"));
    }

    private void OnAddUser(){
        if(_userInputField.text != ""){
            if(_userInputField.text[0] == '!'){
                switch (_userInputField.text){
                    case "!refresh":
                        SaveLinks();
                        SceneManager.LoadScene(0);
                        break;
                    case "!removeall":
                        users = new List<string>();
                        OnRemoveUser();
                        break;
                    case "!removelast":
                        if(users.Count > 0){
                            OnDebugLog(users[users.Count-1]+" removed");
                            users.RemoveAt(users.Count-1);
                            OnRemoveUser();
                        }
                        break;
                    case "!clear":
                        ClearChat();
                        break;
                    case "!savelinks":
                        SaveLinks();
                        break;
                    case "!opendata":
                        Application.OpenURL(Application.persistentDataPath);
                        break;
                    case "!backup":
                        StartCoroutine("BackupUserFilter");
                        break;
                    case "!urlcount":
                        OnDebugLog("URL filter lenght: "+_dontRepeatLink.Count);
                        break;
                    default:
                        OnDebugLog("Command invalid: "+_userInputField.text);
                        break;
                }
                _userInputField.text = "";
            }
            else{
                users.Add(_userInputField.text.ToLower());
                if(_filterUsers.text.Length > 0){
                    _filterUsers.text += "\n" + _userInputField.text.ToLower();
                }
                else{
                    _filterUsers.text = _userInputField.text.ToLower();
                }
                
                _userInputField.text = "";
            }
        }
        SaveFilterList();
    }

    private void OnRemoveUser(){
        if(_userInputField.text != ""){
            _filterUsers.text = "";
            List<string> oldList = users;
            users = new List<string>();
            foreach (string item in oldList){
                if(!item.Contains(_userInputField.text)){
                    users.Add(item);
                    if(_filterUsers.text.Length > 0){
                        _filterUsers.text += "\n" + item;
                    }
                    else{
                        _filterUsers.text = item;
                    }
                }
            }
            if (users.Count == 0){
                _filterUsers.text = "";
            }
            _userInputField.text = "";
        }
        SaveFilterList();
    }

    private void OnAddChannel(){
        if(_channelInputField.text != ""){
            if(_channelInputField.text[0] == '!'){
                switch (_channelInputField.text){
                    case "!refresh":
                        OnDebugLog("Refreshing Application...");
                        SceneManager.LoadScene(0);
                        break;
                    case "!leaveall":
                        channels = new List<string>();
                        OnRemoveChannel();
                        break;
                    case "!leavelast":
                        if(channels.Count > 0){
                            OnDebugLog(channels[channels.Count-1]+" leaved");
                            channels.RemoveAt(channels.Count-1);
                            OnRemoveChannel();
                        }
                        break;
                    case "!clear":
                        foreach (Transform item in _parent){
                            Destroy(item.gameObject);
                        }
                        break;
                    case "!backup":
                        StartCoroutine("BackupChannelList");
                        break;
                    case "!playing":
                        GameBeingPlayed();
                        break;
                    default:
                        OnDebugLog("Command invalid");
                        break;
                }
                _channelInputField.text = "";
            }
            else{
                channels.Add(_channelInputField.text.ToLower());
                JointChat(_channelInputField.text);
                if(_channelList.text.Length > 0){
                    _channelList.text += "\n" + _channelInputField.text.ToLower();
                }
                else{
                    _channelList.text += _channelInputField.text.ToLower();
                }
                _channelInputField.text = "";
            }
        }
        SaveChannelList();
    }

    private void OnRemoveChannel(){
        if(_channelInputField.text != ""){
            OnDebugLog(_channelInputField.text+" leaved");
            _channelList.text = " ";
            List<string> oldList = channels;
            channels = new List<string>();
            foreach (string item in oldList){
                if (!item.Contains(_channelInputField.text)){
                    channels.Add(item);
                    if(_channelList.text.Length > 0){
                        _channelList.text += "\n" + item;
                    }
                    else{
                        _channelList.text += item;
                    }
                }
            }
            LeaveChat(_channelInputField.text);
            if(channels.Count == 0){
                _channelList.text = "";
            }
            _channelInputField.text = "";
        }
        SaveChannelList();
    }

    private void SaveFilterList(){
        string _compile = "";
        for (int i = 0; i < users.Count; i++){
            _compile += users[i] + "|";
        }

        if(_compile != ""){
            _compile = _compile.Remove(_compile.Length - 1);
        } 
        PlayerPrefs.SetString("activeFilter", _compile);
    }

    private void SaveChannelList(){
        string _compile = "";
        for (int i = 0; i < channels.Count; i++){
            _compile += channels[i] + "|";
        }

        if(_compile != ""){
            _compile = _compile.Remove(_compile.Length - 1);
        } 
        PlayerPrefs.SetString("channels", _compile);
    }

    private void JointChat(string _channelName){

        foreach (TwitchIRC item in _channelsChat){
            if(item.channelName.Contains(_channelName)){
                item.enabled = true;
                return;
            }
        }

        TwitchIRC _instance = gameObject.AddComponent<TwitchIRC>();
        _instance.channelName = _channelName;
        _instance.messageRecievedEvent.AddListener(OnChatMsgReceived);
        _channelsChat.Add(_instance);
    }

    private void LeaveChat(string _channelName){
        if (_channelName.Contains("!leavelast")){
            _channelsChat[_channelsChat.Count - 1].enabled = false;
            _channelsChat[_channelsChat.Count - 1].RemoveChatActivity();
        }
        else if(_channelName.Contains("!leaveall")){
            foreach (TwitchIRC item in _channelsChat){
                item.enabled = false;
                item.RemoveChatActivity();
                _chatActivity = new List<Activity>();
            }
        }
        else{
            foreach (TwitchIRC item in _channelsChat){
                if(_channelName.Contains(item.channelName)){
                    item.channelIsLive = false;
                    item.enabled = false;
                    item.RemoveChatActivity();
                }
            }
        }
    }

    private void SaveLinks(){
        new Thread(new ThreadStart(delegate{
            if (_dontRepeatLink.Count > 0){
                if (System.IO.File.Exists(persistentDataPath + "/filteredUrls.txt")){
                    System.IO.File.Delete(persistentDataPath + "/filteredUrls.txt");
                }
                System.IO.FileStream _fs = new System.IO.FileStream(persistentDataPath + "/filteredUrls.txt", System.IO.FileMode.CreateNew);
                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(_fs)){
                    foreach (ClipInfo item in _dontRepeatLink){
                        TimeSpan _timeSpan = DateTime.Now - DateTime.Parse(item.time);
                        if(_timeSpan.TotalHours < 24){
                            sw.WriteLine(item.id + "|" + item.time);
                        }
                    }
                    sw.Close();
                }
                _fs.Dispose();
            }
            Thread.CurrentThread.Abort();
        })).Start();
        OnDebugLog("Filter links are saved.");
    }

    public void RefreshChat(string _channelName){
        StartCoroutine(HandleRefreshChat(_channelName));
    }

    public void RefreshChatActivity(Activity _value){
        List<Activity> _refresh = _chatActivity;
        _chatActivity = new List<Activity>();
        foreach (Activity item in _refresh){
            if(!item.name.Contains(_value.name) && item.isLive){
                _chatActivity.Add(item);
            }
        }
        _chatActivity.Add(_value);
        _chatActivity.Sort((x,y) => x.time.CompareTo(y.time));
        _logConnection.text = "";
        foreach (Activity item in _chatActivity){
            _logConnection.text += "\n" + item.name;
        }
    }

    public void ClearChat(){
        foreach (Transform item in _parent){
            Destroy(item.gameObject);
        }
    }

    public void ConnectedChats(){
        OnDebugLog("Connected to "+_chatActivity.Count+" chat"+(_chatActivity.Count > 1 ? "s":"")+".");
    }

    public void GameBeingPlayed(){
        foreach (TwitchIRC item in _channelsChat){
            item.PlayingGame();
        }
    }

    private IEnumerator CheckLink(string _url, GameObject _instance){
        _instance.SetActive(false);
        WWW _request = new WWW(_url);
        yield return _request;
        if(_request.error != null){
            yield break;
        }

        string _html = _request.text;

        if(!_html.Split('\n')[3].Contains("meta charset=")){
            string _id = "";
            string _time = null;

            foreach (string item in _html.Split('\n')){
                if(item.Contains("broadcaster_login:")){
                    _id = item.Split('"')[1];
                }
                else if (item.Contains("created_at:")){
                    _time = item.Split('"')[1].Substring(0,16);
                    break;
                }
            }

            if(_time == null){
                foreach (string item in ignoreUrl){
                    if(item.Contains(_url)){
                        if(_instance != null){
                            Destroy(_instance);
                            yield break;
                        }
                    }
                }

                if(_instance != null){
                    _urlSound.Play();
                    Windows.FlashWindow();
                    _instance.SetActive(true);
                    yield break;
                }
            }

            TimeSpan _differenceTime = DateTime.Now - DateTime.Parse(_time);
            if(_differenceTime.TotalHours > 10){
                if(_instance != null){
                    Destroy(_instance);
                }
                yield break;
            }

            bool duplicated = false;
            bool threadDone = false;
            new Thread(new ThreadStart(delegate{
                for (int i = 0; i < _dontRepeatLink.Count; i++){
                    if(_dontRepeatLink[i].id.Contains(_id) && _dontRepeatLink[i].time.Contains(_time)){
                        duplicated = true;
                        break;
                    }
                }
                threadDone = true;
            })).Start();

            yield return new WaitUntil(()=>threadDone);

            if(duplicated){
                if(_instance != null){
                    Destroy(_instance);
                }
                
            }
            else{
                _dontRepeatLink.Add(new ClipInfo(){id = _id, time = _time});
                _urlSound.Play();
                Windows.FlashWindow();
                _instance.SetActive(true);
            }
        }
        else{
            if(_instance != null){
                Destroy(_instance);
            }
        }
    }

    private IEnumerator QueueChatJoin(){
        foreach (string item in channels){
            JointChat(item);
            yield return new WaitForEndOfFrame();
        }
        yield return true;
    }

    public IEnumerator HandleRefreshChat(string _channelName){
        LeaveChat(_channelName);
        yield return new WaitForSeconds(1f);
        JointChat(_channelName);
    }

    private IEnumerator BackupUserFilter(){
        bool _flag = false;
        new Thread(new ThreadStart(delegate{
            if(System.IO.File.Exists(persistentDataPath+"/userFilterBKP.txt")){
                System.IO.File.Delete(persistentDataPath+"/userFilterBKP.txt");
            }
            System.IO.FileStream fs = new System.IO.FileStream(persistentDataPath+"/userFilterBKP.txt",System.IO.FileMode.CreateNew);
            using (System.IO.StreamWriter sw = new System.IO.StreamWriter(fs)){
                foreach(string item in users){
                    sw.WriteLine(item);
                }
                sw.Close();
            }
            fs.Dispose();
            _flag = true;
            Thread.CurrentThread.Abort();
        })).Start();
        yield return new WaitUntil(() => _flag);
        OnDebugLog("'User List' Backup is done!");
    }

    private IEnumerator BackupChannelList(){
        bool _flag = false;
        new Thread(new ThreadStart(delegate{
            if(System.IO.File.Exists(persistentDataPath+"/channelListBKP.txt")){
                System.IO.File.Delete(persistentDataPath+"/channelListBKP.txt");
            }
            System.IO.FileStream fs = new System.IO.FileStream(persistentDataPath+"/channelListBKP.txt",System.IO.FileMode.CreateNew);
            using (System.IO.StreamWriter sw = new System.IO.StreamWriter(fs)){
                foreach(string item in channels){
                    sw.WriteLine(item);
                }
                sw.Close();
            }
            fs.Dispose();
            _flag = true;
            Thread.CurrentThread.Abort();
        })).Start();
        yield return new WaitUntil(() => _flag);
        OnDebugLog("'Channel List' Backup is done!");
    }

    /*public void OnSubmit()
    {
        if (inputField.text.Length > 0)
        {
            IRC.SendMsg(inputField.text); //send message.
            CreateUIMessage(IRC.nickName, inputField.text); //create ui element.
            inputField.text = "";
        }
    }*/
}