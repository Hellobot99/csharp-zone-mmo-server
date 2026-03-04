using GameProto;
using Google.Protobuf;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChatUI : MonoBehaviour
{
    [SerializeField] private GameObject chatPanel;
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text toggleButtonText;
    [SerializeField] private TMP_InputField messageInput;
    [SerializeField] private Button sendButton;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform messageContainer;
    [SerializeField] private TMP_Text messagePrefab;

    private void Start()
    {
        chatPanel.SetActive(false);
        if (toggleButtonText != null) toggleButtonText.text = "Chat ▲";

        toggleButton.onClick.AddListener(ToggleChat);
        sendButton.onClick.AddListener(OnSendClicked);
        messageInput.onSubmit.AddListener(_ => OnSendClicked());

        if (NetworkManager.Instance != null)
            NetworkManager.Instance.Dispatcher.Register(PacketType.ChatBroadcast, OnChatBroadcast);
        else
            Debug.LogError("[ChatUI] NetworkManager.Instance is null. Start from LoginScene.");
    }

    private void ToggleChat()
    {
        bool next = !chatPanel.activeSelf;
        chatPanel.SetActive(next);
        if (toggleButtonText != null)
            toggleButtonText.text = next ? "Chat ▼" : "Chat ▲";
    }

    private void OnSendClicked()
    {
        string text = messageInput.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        if (NetworkManager.Instance == null)
        {
            Debug.LogError("[ChatUI] NetworkManager is null");
            return;
        }

        var req = new ChatRequest { Message = text };
        NetworkManager.Instance.Send(PacketType.ChatRequest, req.ToByteArray());

        messageInput.text = string.Empty;
        messageInput.ActivateInputField();
    }

    private void OnChatBroadcast(byte[] body)
    {
        var broadcast = ChatBroadcast.Parser.ParseFrom(body);
        AddMessage($"[{broadcast.Username}]: {broadcast.Message}");
    }

    private void AddMessage(string text)
    {
        var msg = Instantiate(messagePrefab, messageContainer);
        msg.text = text;

        // 레이아웃 즉시 갱신 후 맨 아래로 스크롤
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }
}
