using System.Collections;
using System.Text;
using Model;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class SigningController : MonoBehaviour
{
    private const string API_URL = "http://localhost:8081";
    public TMP_Text resultMessage;
    public TMP_InputField username;
    public TMP_InputField password;

    public static UserToken userToken;

    public void PostSignUpData()
    {
        ChatUser user = new ChatUser();
        user.username = username.text;
        user.password = password.text;
        string jsonBody = JsonUtility.ToJson(user);

        StartCoroutine(PostSignUpData_Coroutine(jsonBody));
    }
    
    public void PostSignInData()
    {
        ChatUser user = new ChatUser();
        user.username = username.text;
        user.password = password.text;
        string jsonBody = JsonUtility.ToJson(user);

        StartCoroutine(PostSignInData_Coroutine(jsonBody));
    }

    IEnumerator PostSignUpData_Coroutine(string jsonBody)
    {
        /*
        using (UnityWebRequest request = UnityWebRequest.Put(API_URL + "/register", jsonBody))
        {
            request.method = "POST";
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            
            resultMessage.text = request.downloadHandler.text;
        }*/
        UnityWebRequest request = new UnityWebRequest(API_URL + "/signup", "POST");
        byte[] rawBody = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(rawBody);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();
        
        if (request.responseCode == 200)
        {
            resultMessage.color = Color.green;
            resultMessage.text = request.downloadHandler.text;
        }
        else
        {
            resultMessage.color = Color.red;
            resultMessage.text = request.downloadHandler.text;
        }
        request.Dispose();
    }

    IEnumerator PostSignInData_Coroutine(string jsonBody)
    {
        UnityWebRequest request = new UnityWebRequest(API_URL + "/login", "POST");
        byte[] rawBody = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = (UploadHandler)new UploadHandlerRaw(rawBody);
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.responseCode == 200)
        {
            userToken = JsonUtility.FromJson<UserToken>(request.downloadHandler.text);
            SceneManager.LoadSceneAsync("ChatScene");
        }
        else
        {
            resultMessage.color = Color.red;
            resultMessage.text = request.downloadHandler.text;
        }
        request.Dispose();
    }
}
