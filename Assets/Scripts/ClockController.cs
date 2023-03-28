using Assets.Scripts;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ClockController : MonoBehaviour
{
    #region Fields
    [SerializeField] private Transform _hourArrow;
    [SerializeField] private Transform _minuteArrow;
    [SerializeField] private Transform _secondArrow;
    [SerializeField] private TextMeshProUGUI _timeText;
    [SerializeField] private TextMeshProUGUI _alarmTimeText;
    [SerializeField] private Button _setAlarmButton;
    [SerializeField] private Button _alarmTimeButton;

    [SerializeField] private Button _wakeupButton;
    [SerializeField] private GameObject _digitalAlarmPanel;
    [SerializeField] private TMP_InputField _dayInput;
    [SerializeField] private TMP_InputField _hourInput;
    [SerializeField] private TMP_InputField _minInput;
    [SerializeField] private float _arrowsSpeed;

    private const string TIME_SOURCE_TIMEAPI = "https://timezonedb.com/";
    private const string TIME_SOURCE_TIMEAPI1 = "https://www.timeanddate.com/";
    private const string TIME_SOURCE_YANDEX = "https://yandex.ru";

    private DateTime _internetTime;
    private DateTime _currentTime;

    private DateTime _alarmTime;
    private int _alarmHour;
    private int _alarmMinute;

    private float timeInterval = 3600f;
    private bool _isTicked;
    float hourPos;
    float minutePos;
    private bool setAlarm = false;

    private Vector3 _prevMousePosition;
    private float _rotationSpeed;
    
    #endregion

    private void Awake()
    {
        _internetTime = CheckGlobalTime(TIME_SOURCE_TIMEAPI);
        InvokeRepeating(nameof(CheckTime), timeInterval, timeInterval);
        _setAlarmButton.onClick.AddListener(SetAlarm);
        _wakeupButton.onClick.AddListener(StopAlarm);
        _alarmTimeButton.onClick.AddListener(ShowDayTimeRequestPanel);
        _dayInput.onSelect.AddListener(ShowKeyboard);
        _hourInput.onSelect.AddListener(ShowKeyboard);
        _minInput.onSelect.AddListener(ShowKeyboard);
    }

    private void Update()
    {
        UpdateArrowsPosition();
        Alarm();
    }
    private DateTime CheckGlobalTime(string uri)
    {
        var request = UnityWebRequest.Head(uri);
        request.SendWebRequest();

        while (!request.isDone) { }

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.Log($"Error: {request.error} from {request.uri}");

            switch (uri)
            {
                case TIME_SOURCE_TIMEAPI:
                    return CheckGlobalTime(TIME_SOURCE_TIMEAPI1);
                case TIME_SOURCE_TIMEAPI1:
                    return CheckGlobalTime(TIME_SOURCE_YANDEX);
                case TIME_SOURCE_YANDEX:
                    return DateTime.MinValue;
                default:
                    return DateTime.MinValue;
            }
        }
        Debug.Log($"Time received from {request.uri}");
        var str = request.GetResponseHeader("Date");

        if (!DateTime.TryParse(str, out DateTime dateTime))
            return DateTime.MinValue;

        return dateTime.ToLocalTime();
    }
    private void CheckTime()
    {
        _internetTime = CheckGlobalTime(TIME_SOURCE_TIMEAPI);
    }
    private void SetAlarm()
    {
        if (setAlarm && !_digitalAlarmPanel.activeSelf)
        {
            SetAlarmByArrows();
        }
        if (setAlarm && _digitalAlarmPanel.activeSelf)
        {
            SetAlarmByDigits();
        }
        setAlarm = !setAlarm;
        _digitalAlarmPanel.SetActive(false);
    }

    private void UpdateArrowsPosition()
    {
        if (!setAlarm)
        {
            _currentTime = _internetTime.AddSeconds(Time.realtimeSinceStartup);

            float hours = (float)_currentTime.Hour;
            float minutes = (float)_currentTime.Minute;
            float seconds = (float)_currentTime.Second + (float)_currentTime.Millisecond / 1000f;

            _hourArrow.localEulerAngles = new Vector3(0, 0, -hours * 30f - minutes * 0.5f);
            _minuteArrow.localEulerAngles = new Vector3(0, 0, -minutes * 6f - seconds * 0.1f);
            _secondArrow.localEulerAngles = new Vector3(0, 0, -seconds * 6f);
            _timeText.text = $"{_currentTime.ToString("dd.MM.yy")}\n{_currentTime.ToString("HH:mm:s")}";

            hourPos = _hourArrow.localEulerAngles.z;
            minutePos = _minuteArrow.localEulerAngles.z;
        }
        if (setAlarm)
        {
            if (Input.GetMouseButtonDown(0))
            {
                _prevMousePosition = Input.mousePosition;
            }

            if (Input.GetMouseButton(0))
            {
                Vector3 mouseDelta = Input.mousePosition - _prevMousePosition;
                _rotationSpeed = Mathf.Clamp(mouseDelta.magnitude / Time.deltaTime, 0f, _arrowsSpeed);

                RotateClockArrows(mouseDelta);

                _prevMousePosition = Input.mousePosition;
            }
        }
    }
    private void RotateClockArrows(Vector3 delta)
    {
        float hourAngle = Mathf.Sign(delta.y) * delta.y * 0.5f * _rotationSpeed;
        float minuteAngle = Mathf.Sign(delta.y) * delta.y * 6f * _rotationSpeed;

        _hourArrow.Rotate(Vector3.back, hourAngle, Space.Self);
        _minuteArrow.Rotate(Vector3.back, minuteAngle, Space.Self);

        if (_hourArrow.localEulerAngles.z % 360 >= 358 && _hourArrow.localEulerAngles.z % 360 <= 363 && !_isTicked)
        {
            _isTicked = true;
        }
    }
    private void SetAlarmByArrows()
    {
        float hourAngle = _hourArrow.transform.localEulerAngles.z;
        float minuteAngle = _minuteArrow.transform.localEulerAngles.z;

        if (hourPos == hourAngle && minuteAngle == minutePos)
        {
            return;
        }

        _alarmTime = _currentTime;

        if (_isTicked)
        {
            _alarmTime = _alarmTime.AddDays(1);
            _isTicked = false;
        }

        hourAngle = (-hourAngle + 360f) % 360f;

        _alarmHour = Mathf.FloorToInt(hourAngle / 30f);

        if (_currentTime.Hour > 12 && _alarmTime.Day == _currentTime.Day)
        {
            _alarmHour += 12;
        }

        minuteAngle = (-minuteAngle + 360f) % 360f;

        _alarmMinute = Mathf.FloorToInt(minuteAngle / 6f);

        _alarmTime = _alarmTime.Date + new TimeSpan(_alarmHour, _alarmMinute, _currentTime.Second);

        _alarmTimeText.text = $"Alarm set\n{_alarmTime.ToString("d")}\n{_alarmTime.ToString("HH:mm:s")}";
    }
    private void SetAlarmByDigits()
    {
        int.TryParse(_dayInput.text, out int _alarmDay);
        if (_alarmDay <= 0 || _alarmDay > 31)
        {
            _alarmDay = _currentTime.Day;
        }

        int.TryParse(_hourInput.text, out int _alarmHour);
        if (_alarmHour <= 0 || _alarmHour > 23)
        {
            _alarmHour = _currentTime.Hour + 1;
        }

        int.TryParse(_minInput.text, out int _alarmMinute);
        if (_alarmMinute <= 0 || _alarmMinute > 59)
        {
            _alarmMinute = _currentTime.Minute + 2;
        }

        _alarmTime = new DateTime(_currentTime.Year,_currentTime.Month, _alarmDay, _alarmHour, _alarmMinute, _currentTime.Second);
        _alarmTimeText.text = $"Alarm set\n{_alarmTime.ToString("d")}\n{_alarmTime.ToString("HH:mm:s")}";
    }
    private void ShowDayTimeRequestPanel()
    {
        if (setAlarm)
        {
            _digitalAlarmPanel.SetActive(true);
        }
    }
    private void ShowKeyboard(string value)
    {
        TouchScreenKeyboard.Open("", TouchScreenKeyboardType.NumberPad);
    }
    private void Alarm()
    {
        if (_alarmTime != default && !setAlarm)
        {
            TimeSpan difference = _alarmTime - _currentTime;
            if (difference.TotalMilliseconds <= 0)
            {
                _wakeupButton.gameObject.SetActive(true);
            }
        }
    }
    private void StopAlarm()
    {
        _alarmTime = default;
        _wakeupButton.gameObject.SetActive(false);
        _alarmTimeText.text = "Alarm set";
    }
}

