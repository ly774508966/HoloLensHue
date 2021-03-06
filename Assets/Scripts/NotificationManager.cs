﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using UnityEngine.UI;

public class NotificationManager : Singleton<NotificationManager> {

    public delegate void NewNotification(Notification notification, Color color);
    public static event NewNotification newNotification;

    public delegate void NewMenu(Menu menu);
    public static event NewMenu newMenu;

    public delegate void NotificationCanceled();
    public static event NotificationCanceled notificationCanceled;

    // Time notification should be displayed. Void if notification requires user action
    private static float TimeTillExpiration = 8f;

    private static Canvas canvas;
    private static Color color;

    private static GameObject panelBorderGO;
    private static GameObject nextButtonGO;
    private static GameObject backButtonGO;
    private static GameObject saveButtonGO;
    private static GameObject finishButtonGO;
    private static GameObject mainMenuGO;

    private static NotificationManager notificationManager;

    private static Coroutine coroutine;
    private static bool notificationActive;
    private static bool menuActive;

    private GameObject hueBridgeGO;
    private HueBridgeManager hueBridgeManager;

    public bool bridgeInited;

    // Add an audio clip to the Popup Clips in SoundManager and specify name here 
    private string buttonClickedSound = "click1";

    void Awake()
    {
        notificationManager = this;
    }

    void OnEnable()
    {
        MenuStateManager.onMenuChanged += DisplayMenu;
        HueBridgeManager.restartSearchForBridge += ResetBridgeInited;
    }

    void OnDisable()
    {
        MenuStateManager.onMenuChanged -= DisplayMenu;
        HueBridgeManager.restartSearchForBridge -= ResetBridgeInited;
    }

    void Start() {

        foreach (Transform child in transform)
        {
            if (child.name == "Canvas")
            {
                GameObject canvasGO = child.gameObject;
                canvas = canvasGO.GetComponent<Canvas>();
                canvas.enabled = false;

                // assign UI elements to gameObjects for activating/deactivating
                foreach (Transform grandchild in canvasGO.transform)
                {
                    if (grandchild.name == "PanelBorder")
                    {
                        panelBorderGO = grandchild.gameObject;
                    }
                    else if (grandchild.name == "NextButtonBorder")
                    {
                        nextButtonGO = grandchild.gameObject;
                    }
                    else if (grandchild.name == "BackButtonBorder")
                    {
                        backButtonGO = grandchild.gameObject;
                    }
                    else if (grandchild.name == "SaveButtonBorder")
                    {
                        saveButtonGO = grandchild.gameObject;
                    }
                    else if (grandchild.name == "FinishButtonBorder")
                    {
                        finishButtonGO = grandchild.gameObject;
                    }
                    else if (grandchild.name == "MainMenu")
                    {
                        mainMenuGO = grandchild.gameObject;
                    }
                }
            }
        }

        if (!canvas)
        {
            Debug.Log("No child Canvas was found. Please add one to use notification system.");
        }

        // TODO create a more reliable solution - used to prevent null object reference
        hueBridgeGO = GameObject.Find("AppManager");
        hueBridgeManager = hueBridgeGO.GetComponent<HueBridgeManager>();

        // TODO this should not be called from this file. Need a better system for initializing
        // hueBridgeManager.InitHueBridgeManager();
        hueBridgeManager.InitMainMenu();
    }

    // updating for testing only
    void Update()
    {
        if (Input.GetKeyDown("t"))
        {
            DismissAction();
        }
    }

    public static void DisplayNotification(Notification notification)
    {
        if (newNotification != null)
        {
            canvas.enabled = true;

            if (notification.Type == "error")
            {
                color = Color.red;
                SoundManager.instance.PlayNotificationPopup("beepup");

            } else if (notification.Type == "alert")
            {
                // color = steelblue
                color = new Color(0.27f, 0.5f, 0.7f);
                SoundManager.instance.PlayNotificationPopup("tonebeep");
            }

            if (notification.SendToConsole)
            {
                Debug.Log(notification.Message);
            }

            newNotification(notification, color);

            if (!notification.RequiresAction)
            {
                // if notification is active, we discard the previous expiration timer before we start a new one
                if (notificationActive)
                {
                    notificationManager.StopCoroutine(coroutine);
                }
                notificationActive = true;

                var expiration = TimeTillExpiration;
                if (notification.Expiration != 0)
                {
                    expiration = notification.Expiration;
                }
                coroutine = notificationManager.StartCoroutine(NotificationExpiration(expiration));
            }
            else
            {
                // TODO handle notifications that require a user action
            }
        }
    }

    public static void CancelNotification()
    {
        // if an expiration timer is running, we want to cancel it first so it does not affect the next menu/notif
        if (notificationActive)
        {
            notificationManager.StopCoroutine(coroutine);
        }

        canvas.enabled = false;
        notificationActive = false;

        if (notificationCanceled != null)
        {
            notificationCanceled();
        }

    }

    public static void DisplayMenu(Menu menu)
    {   
        if (menu != null)
        {

            if (newMenu != null)
            {
                newMenu(menu);
            }

            canvas.enabled = true;
            if (menu.Name == "MainMenu")
            {
                mainMenuGO.SetActive(true);
            }
            else if (menu.Name == "HideMenu")
            {
                mainMenuGO.SetActive(false);
            }
            else // displays the common panel board if not the main menu
            {
                RectTransform rt = panelBorderGO.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(menu.Width, menu.Height);

                BoxCollider col = panelBorderGO.GetComponent<BoxCollider>();
                col.size = new Vector3(menu.Width, menu.Height, 1);

                // displays the proper menu content in the panelBorder
                foreach (Transform child in panelBorderGO.transform)
                {
                    if (child.name == menu.Name)
                    {
                        child.gameObject.SetActive(true);
                    }
                    else
                    {
                        // TextPanel should always be active for general notifications. Hiding/showing this object done through Image Component
                        if (child.name != "TextPanel")
                        {
                            // ensures only one submenu panel is open at a time
                            child.gameObject.SetActive(false);
                        }               
                    }
                }
            }

            //SoundManager.instance.PlayNotificationPopup("click1");
            //newMenu(menu);
        }

        if (!menu.RequiresAction)
        {
            // if menu is active, we discard the previous expiration timer before we start a new one
            if (menuActive)
            {
                notificationManager.StopCoroutine(coroutine);
            }
            menuActive = true;

            var expiration = TimeTillExpiration;
            if (menu.Expiration != 0)
            {
                expiration = menu.Expiration;
            }
            coroutine = notificationManager.StartCoroutine(NotificationExpiration(expiration));
        }

        // if a next button is needed, we will display it below the menu main content panel
        if (menu.NextButton)
        {
            nextButtonGO.SetActive(true);
            RectTransform nextRt = nextButtonGO.GetComponent<RectTransform>();
            nextRt.localPosition = new Vector3(nextRt.localPosition.x, menu.ButtonPosY, nextRt.localPosition.z);
        }
        else
        {
            // this ensures the previous menu's next button does not carry over
            nextButtonGO.SetActive(false);
        }

        // if a back button is needed, we will display it below the menu main content panel
        if (menu.BackButton)
        {
            backButtonGO.SetActive(true);
            RectTransform backRt = backButtonGO.GetComponent<RectTransform>();
            backRt.localPosition = new Vector3(backRt.localPosition.x, menu.ButtonPosY, backRt.localPosition.z);
        }
        else
        {
            // this ensures the previous menu's back button does not carry over
            backButtonGO.SetActive(false);
        }

        // if a save button is needed, we will display it below the menu main content panel
        if (menu.SaveButton)
        {
            saveButtonGO.SetActive(true);
            RectTransform saveRt = saveButtonGO.GetComponent<RectTransform>();
            saveRt.localPosition = new Vector3(saveRt.localPosition.x, menu.ButtonPosY, saveRt.localPosition.z);
        }
        else
        {
            // this ensures the previous menu's back button does not carry over
            saveButtonGO.SetActive(false);
        }

        // if a finish button is needed, we will display it below the menu main content panel
        if (menu.FinishButton)
        {
            finishButtonGO.SetActive(true);
            RectTransform finishRt = finishButtonGO.GetComponent<RectTransform>();
            finishRt.localPosition = new Vector3(finishRt.localPosition.x, menu.ButtonPosY, finishRt.localPosition.z);
        }
        else
        {
            // this ensures the previous menu's back button does not carry over
            finishButtonGO.SetActive(false);
        }
    }

    public static void DetachMenu()
    {
        canvas.GetComponent<Tagalong>().enabled = false;
        canvas.GetComponent<FixedAngularSize>().enabled = false;
    }

    public static void AttachMenu()
    {
        canvas.GetComponent<Tagalong>().enabled = true;
        canvas.GetComponent<FixedAngularSize>().enabled = true;
    }

    public static void HideSubMenuPanel()
    {
        foreach (Transform child in panelBorderGO.transform)
        {
            if (child.name == "SetupFinished")
            {
                child.gameObject.SetActive(false);
            }
        }
    }
    private static IEnumerator NotificationExpiration(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        CancelNotification();
    }

    public void DismissAction()
    {
        MenuStateManager.Instance.CurrentState = MenuStateManager.MenuState.Hidden;
        // prevents instantiated duplicate orbs
        if (!bridgeInited)
        {
            //TODO tie this to StateManager to abstract out direct function calls
            hueBridgeManager.InitHueBridgeManager();
        }

        StateManager.Instance.CurrentState = StateManager.HueAppState.Ready;
        SoundManager.instance.PlayNotificationPopup(buttonClickedSound);

        HidePanelSubItems();
        panelBorderGO.GetComponent<Image>().enabled = false;
        panelBorderGO.GetComponent<BoxCollider>().enabled = false;
    }

    public void TutorialAction()
    {
        mainMenuGO.SetActive(false);

        MenuStateManager.Instance.CurrentState = MenuStateManager.MenuState.TT_Interactions;

        SoundManager.instance.PlayNotificationPopup(buttonClickedSound);
    }

    public void FinishAction()
    {
        if (notificationCanceled != null)
        {
            notificationCanceled();
        }

        foreach (Transform child in panelBorderGO.transform)
        {
            // TextPanel should always be active for general notifications. Hiding/showing this object done through Image Component
            if (child.name != "TextPanel")
            {
                child.gameObject.SetActive(false);
            }         
        }
        MenuStateManager.Instance.CurrentState = MenuStateManager.MenuState.TTFinished;
        MenuStateManager.Instance.CurrentState = MenuStateManager.MenuState.MainMenu;

        SoundManager.instance.PlayNotificationPopup(buttonClickedSound);
    }

    public void NextAction()
    {
        MenuStateManager.Instance.CurrentState++;

        SoundManager.instance.PlayNotificationPopup(buttonClickedSound);
    }

    public void BackAction()
    {
        MenuStateManager.Instance.CurrentState--;

        SoundManager.instance.PlayNotificationPopup(buttonClickedSound);
    }

    public void SetupAction()
    {
        mainMenuGO.SetActive(false);

        StateManager.Instance.CurrentState = StateManager.HueAppState.SetupMode;
        if (!bridgeInited)
        {
            hueBridgeManager.InitHueBridgeManager();
        }
        else
        {
            MenuStateManager.Instance.CurrentState = MenuStateManager.MenuState.LinkSuccess;
        }
        bridgeInited = true;

        SoundManager.instance.PlayNotificationPopup(buttonClickedSound);
    }

    public void SaveAction()
    {
        MenuStateManager.Instance.CurrentState = MenuStateManager.MenuState.SetupFinished;
        StateManager.Instance.CurrentState = StateManager.HueAppState.Starting;

        SoundManager.instance.PlayNotificationPopup(buttonClickedSound);
    }

    private void ResetBridgeInited()
    {
        bridgeInited = false;
    }

    public void HidePanelSubItems()
    {
        foreach (Transform child in panelBorderGO.transform)
        {
            // TextPanel should always be active for general notifications. Hiding/showing this object done through Image Component
            if (child.name != "TextPanel")
            {
                child.gameObject.SetActive(false);
            }
        }
    }
}
