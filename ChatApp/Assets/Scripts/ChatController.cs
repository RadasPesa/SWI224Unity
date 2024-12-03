using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Model;
using Newtonsoft.Json;
using StompHelper;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using WebSocketSharp;

public static class ButtonExtension
{
    public static void AddEventListener<T>(this Button button, T param, Action<T> OnClick)
    {
        button.onClick.AddListener(delegate
        {
            OnClick(param);
        });
    }
}

public class ChatController : MonoBehaviour
{
    public GameObject chatSelection;
    public GameObject chatWindow;
    
    public GameObject chatRoomButton_template;
    public TMP_Text chatContentOther_template;
    public TMP_Text chatContentSelf_template;
    
    private const string API_URL = "http://localhost:8081";
    private ChatRoomJson chatRooms;
    private List<GameObject> chatRoomsList;
    private Dictionary<int, List<Message>> privateChats;
    // Zprávy na obrazovce (objekty)
    private List<GameObject> messages;

    private WebSocket ws;
    private StompMessageSerializer serializer;

    private bool fetchMessages = false;

    private void Start()
    {
        // pro loading by se v Unity dalo aktivní jen načítací kolečko na začátku
        // a po skončení rutiny by se zneviditelnilo a dalo SetActive chatSelection
        
        //StartCoroutine(FetchChatRooms_Coroutine());
        chatRoomsList = new List<GameObject>();
        privateChats = new Dictionary<int, List<Message>>();
        messages = new List<GameObject>();
        
        ws = new WebSocket("ws://127.0.0.1:8081/ws");
        ws.OnOpen += (sender, e) =>
        {
            Debug.Log("Open");
            serializer = new StompMessageSerializer();

            var connect = new StompMessage("CONNECT");
            ws.Send(serializer.Serialize(connect));

            var sub = new StompMessage("SUBSCRIBE");
            sub["id"] = "sub-0";
            sub["destination"] = "/chatroom/1";
            ws.Send(serializer.Serialize(sub));
        };
        ws.OnError += (sender, e) => Debug.Log("Error: " + e.Message);
        ws.OnMessage += OnPublicMessageReceived;
        ws.Connect();
    }

    private void Update()
    {
        if (fetchMessages)
        {
            StartCoroutine(FetchMessages_Coroutine());
            fetchMessages = false;
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // Odpojit web sockety
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Pripojit web sockety
    }

    void OpenChatRoom(int chatId)
    {
        chatSelection.SetActive(false);
        chatWindow.SetActive(true);
        
        var selfTemplate = chatContentSelf_template.gameObject;
        var otherTemplate = chatContentOther_template.gameObject;
        foreach (var msg in privateChats[chatId])
        {
            GameObject message;
            if (msg.chatUser.userId == SigningController.userToken.userId)
            {
                message = Instantiate(selfTemplate, selfTemplate.transform.parent.gameObject.transform);
            }
            else
            {
                message = Instantiate(otherTemplate,
                    otherTemplate.transform.parent.gameObject.transform);
            }
            message.GetComponent<TMP_Text>().text = msg.content;
            message.SetActive(true);
            
            messages.Add(message);
        }
    }

    public void CloseChatRoom()
    {
        chatWindow.SetActive(false);
        foreach (var message in messages)
        {
            Destroy(message);
        }
        chatSelection.SetActive(true);
    }

    public void SendMessage(TMP_InputField content)
    {
        string messageToSend = content.text.Trim();
        if (messageToSend.Count(c => !Char.IsWhiteSpace(c)) == 0)
        {
            // Nothing to send (empty string)
            content.text = "";
        }
        else
        {
            var payloadMsg = new PayloadMsg() { senderName = SigningController.userToken.username, content = messageToSend, date = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() };
            var broad = new StompMessage("SEND", JsonConvert.SerializeObject(payloadMsg));
            broad["content-type"] = "application/json";
            broad["destination"] = "/app/message";
            ws.Send(serializer.Serialize(broad));

            content.text = "";
        }
    }

    private void OnPublicMessageReceived(object sender, MessageEventArgs e)
    {
        // Vyzvednout zprávu z public-queue-username
        fetchMessages = true;
        Debug.Log("Received: " + e.Data);
    }

    IEnumerator FetchMessages_Coroutine()
    {
        using (UnityWebRequest request =
               UnityWebRequest.Get(API_URL + "/api/queue?userId=" + SigningController.userToken.userId))
        {
            yield return request.SendWebRequest();
            string messagesJson = request.downloadHandler.text;
            Debug.Log(messagesJson);

            var result = JsonUtility.FromJson<MessageJson>("{\"messages\":" + messagesJson + "}");

            var selfTemplate = chatContentSelf_template.gameObject;
            var otherTemplate = chatContentOther_template.gameObject;
            foreach (var msg in result.messages)
            {
                privateChats[msg.chatRoom.chatId].Add(msg);
                GameObject message;
                if (msg.chatUser.userId == SigningController.userToken.userId)
                {
                    message = Instantiate(selfTemplate, selfTemplate.transform.parent.gameObject.transform);
                }
                else
                {
                    message = Instantiate(otherTemplate, otherTemplate.transform.parent.gameObject.transform);
                }
                message.GetComponent<TMP_Text>().text = msg.content;
                message.SetActive(true);
                
                messages.Add(message);
            }
        }
    }

    IEnumerator FetchChatRooms_Coroutine()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(API_URL + "/chatrooms?userId=" + SigningController.userToken.userId))
        {
            yield return request.SendWebRequest();
            string chatRoomsJson = request.downloadHandler.text;

            chatRooms = JsonUtility.FromJson<ChatRoomJson>("{\"json\":" + chatRoomsJson + "}");

            foreach (var chatRoom in chatRooms.json)
            {
                List<Message> msgList = new List<Message>();
                foreach (var message in chatRoom.messages)
                {
                    msgList.Add(message);
                }
                privateChats[chatRoom.chatId] = msgList;
            }
        }
        FillChatRooms();
    }

    void FillChatRooms()
    {
        for (int i = 0; i < chatRooms.json.Length; i++)
        {
            GameObject chatRoom = Instantiate(chatRoomButton_template, chatSelection.transform);
            chatRoom.transform.GetChild(0).GetComponent<TMP_Text>().text = chatRooms.json[i].chatName;
            chatRoom.GetComponent<Button>().AddEventListener(chatRooms.json[i].chatId, OpenChatRoom);
            chatRoomsList.Add(chatRoom);
        }
        Destroy(chatRoomButton_template);
    }

    [Serializable]
    public class MessageJson
    {
        public Message[] messages;
    }

    [Serializable]
    public class ChatRoomJson
    {
        public ChatRoomData[] json;
    }

    [Serializable]
    public class Message
    {
        public int messageId;
        public ChatUser chatUser;
        public ChatRoom chatRoom;
        public string content;
        public string sendTime;
    }

    [Serializable]
    public class ChatRoomData
    {
        public int chatId;
        public Message[] messages;
        public string chatName;
    }

    [Serializable]
    public class ChatRoom
    {
        public int chatId;
        public string chatName;
        public bool isPublic;
    }

    private class PayloadMsg
    {
        public string senderName { get; set; }
        public string receiverName { get; set; }
        public string receiverChatRoomId { get; set; }
        public string content { get; set; }
        public string date { get; set; }
    }
}
