// 日本語対応
using System.Collections.Generic;
using CriWare;
using UnityEngine.SceneManagement;
using System;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 必須1: ADX LE SDK
/// 必須2: StreamingAssetsフォルダ(直下にacf/acb/awbファイル)
/// </summary>
public class CriAudioManager : MonoBehaviour
{
    private static CriAudioManager _instance = null;
    public static CriAudioManager Instance { get => _instance; /*set => _instance = value;*/ }
    
    private float _masterVolume = 1F;
    private float _bgmVolume = 1F;
    private float _seVolume = 1F;
    private float _voiceVolume = 1F;
    private const float diff = 0.01F;

    /// <summary> マスターボリュームが変更された際に呼ばれるEvent </summary>
    public Action<float> MasterVolumeChanged;
    /// <summary> BGMボリュームが変更された際に呼ばれるEvent </summary>
    public Action<float> BGMVolumeChanged;
    /// <summary> SEボリュームが変更された際に呼ばれるEvent </summary>
    public Action<float> SEVolumeChanged;
    /// <summary> Voiceボリュームが変更された際に呼ばれるEvent </summary>
    public Action<float> VoiceVolumeChanged;

    private CriAtomExPlayer _bgmPlayer;
    private CriAtomExPlayback _bgmPlayback;

    private CriAtomExPlayer _sePlayer;
    private CriAtomExPlayer _loopSEPlayer;
    private List<CriPlayerData> _seData;

    private CriAtomExPlayer _voicePlayer;
    private List<CriPlayerData> _voiceData;

    private string _currentBGMCueName = "";
    private CriAtomExAcb _currentBGMAcb = null;

    [SerializeField, Header("acfファイルの名前")] private string _acfFileName = default;
    [SerializeField, Header("BGM用のキューシート")] private string _bGMCueSheet = default;
    [SerializeField, Header("SE用のキューシート")] private string _sECueSheet = default;
    [SerializeField, Header("Voice用のキューシート")] private string _voiceCueSheet = default;

    public enum CueSheetType
    {
        BGM,
        SE,
        Voice
    }
    
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
        
        _bgmPlayer = new CriAtomExPlayer();
        _sePlayer = new CriAtomExPlayer(); 
        _loopSEPlayer = new CriAtomExPlayer(); 
        _seData = new List<CriPlayerData>(); 
        _voicePlayer = new CriAtomExPlayer(); 
        _voiceData = new List<CriPlayerData>();
        Initialize();
        
        
        // CriAtom作成: acbファイルを管理するため
        //var go = new GameObject("New GameObject Have CriAtom").AddComponent<CriAtom>();
        // acfの設定
        var path = Application.streamingAssetsPath + $"/{_acfFileName}.acf";
        CriAtomEx.RegisterAcf(null, path);
        // 使用するacbファイルを追加
        CriAtom.AddCueSheet(_bGMCueSheet, $"{_bGMCueSheet}.acb", $"{_bGMCueSheet}.awb", null);
        CriAtom.AddCueSheet(_sECueSheet, $"{_sECueSheet}.acb", $"{_sECueSheet}.awb", null);
        //CriAtom.AddCueSheet(_voiceCueSheet, $"{_voiceCueSheet}.acb", $"{_voiceCueSheet}.awb", null);
    }

    /// <summary>
    /// キューシートのタイプに応じて、キューシートの名前を返す
    /// </summary>
    /// <returns></returns>
    private string GetCueSheetName(CueSheetType type)
    {
        switch (type)
        {
            case CueSheetType.BGM:
                return _bGMCueSheet;
            case CueSheetType.SE:
                return _sECueSheet;
            case CueSheetType.Voice:
                return _voiceCueSheet;
            default:
                Debug.LogWarning("CueSheetTypeが一致しませんでした。");
                return null;
        }
    }
    
    /// <summary>マスターボリューム</summary>
    /// <value>変更したい値</value>
    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            if (_masterVolume + diff < value || _masterVolume - diff > value)
            {
                MasterVolumeChanged.Invoke(value);
                _masterVolume = value;
            }
        }
    }

    /// <summary>BGMボリューム</summary>
    /// <value>変更したい値</value>
    public float BGMVolume
    {
        get => _bgmVolume;
        set
        {
            if (_bgmVolume + diff < value || _bgmVolume - diff > value)
            {
                BGMVolumeChanged.Invoke(value);
                _bgmVolume = value;
            }
        }
    }

    /// <summary>マスターボリューム</summary>
    /// <value>変更したい値</value>
    public float SEVolume
    {
        get => _seVolume;
        set
        {
            if (_seVolume + diff < value || _seVolume - diff > value)
            {
                SEVolumeChanged.Invoke(value);
                _seVolume = value;
            }
        }
    }

    public float VoiceVolume
    {
        get => _voiceVolume;
        set
        {
            if (_voiceVolume + diff < value || _voiceVolume - diff > value)
            {
                VoiceVolumeChanged.Invoke(value);
                _voiceVolume = value;
            }
        }
    }

    /// <summary>SEのPlayerとPlaback</summary>
    private struct CriPlayerData
    {
        private CriAtomExPlayback _playback;
        private CriAtomEx.CueInfo _cueInfo;


        public CriAtomExPlayback Playback
        {
            get => _playback;
            set => _playback = value;
        }
        public CriAtomEx.CueInfo CueInfo
        {
            get => _cueInfo;
            set => _cueInfo = value;
        }

        public bool IsLoop
        {
            get => _cueInfo.length < 0;
        }
    }

    private void Initialize()
    {
        MasterVolumeChanged += volume =>
        {
            _bgmPlayer.SetVolume(volume * _bgmVolume);
            _bgmPlayer.Update(_bgmPlayback);

            for (int i = 0; i < _seData.Count; i++)
            {
                if (_seData[i].IsLoop)
                {
                    _loopSEPlayer.SetVolume(volume * _seVolume);
                    _loopSEPlayer.Update(_seData[i].Playback);
                }
                else
                {
                    _sePlayer.SetVolume(volume * _seVolume);
                    _sePlayer.Update(_seData[i].Playback);
                }
            }

            for (int i = 0; i < _voiceData.Count; i++)
            {
                _voicePlayer.SetVolume(_masterVolume * volume);
                _voicePlayer.Update(_voiceData[i].Playback);
            }
        };

        BGMVolumeChanged += volume =>
        {
            _bgmPlayer.SetVolume(_masterVolume * volume);
            _bgmPlayer.Update(_bgmPlayback);
        };

        SEVolumeChanged += volume =>
        {
            for (int i = 0; i < _seData.Count; i++)
            {
                if (_seData[i].IsLoop)
                {
                    _loopSEPlayer.SetVolume(_masterVolume * volume);
                    _loopSEPlayer.Update(_seData[i].Playback);
                }
                else
                {
                    _sePlayer.SetVolume(_masterVolume * volume);
                    _sePlayer.Update(_seData[i].Playback);
                }
            }
        };

        VoiceVolumeChanged += volume =>
        {
            for (int i = 0; i < _voiceData.Count; i++)
            {
                _voicePlayer.SetVolume(_masterVolume * volume);
                _voicePlayer.Update(_voiceData[i].Playback);
            }
        };

        SceneManager.sceneUnloaded += Unload;
    }

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= Unload;
    }
    // ここに音を鳴らす関数を書いてください

    /// <summary>BGMを開始する</summary>
    /// <param name="type">流したいキューシートのタイプ</param>
    /// <param name="cueName">流したいキューの名前</param>
    public void PlayBGM(CueSheetType type, string cueName)
    {
        var cueSheetName = GetCueSheetName(type);
        if (cueSheetName == null){return;}
        var temp = CriAtom.GetCueSheet(cueSheetName).acb;

        if (_currentBGMAcb == temp && _currentBGMCueName == cueName &&
            _bgmPlayer.GetStatus() == CriAtomExPlayer.Status.Playing)
        {
            return;
        }

        StopBGM();

        _bgmPlayer.SetCue(temp, cueName);
        _bgmPlayback = _bgmPlayer.Start();
        _currentBGMAcb = temp;
        _currentBGMCueName = cueName;
    }

    /// <summary>BGMを中断させる</summary>
    public void PauseBGM()
    {
        if (_bgmPlayer.GetStatus() == CriAtomExPlayer.Status.Playing)
        {
            _bgmPlayer.Pause();
        }
    }

    /// <summary>中断したBGMを再開させる</summary>
    public void ResumeBGM()
    {
        _bgmPlayer.Resume(CriAtomEx.ResumeMode.PausedPlayback);
    }

    /// <summary>BGMを停止させる</summary>
    public void StopBGM()
    {
        if (_bgmPlayer.GetStatus() == CriAtomExPlayer.Status.Playing)
        {
            _bgmPlayer.Stop();
        }
    }

    /// <summary> SEを流す関数 </summary>
    /// <param name="type"> 流したいキューシートのタイプ </param>
    /// <param name="cueName"> 流したいキューの名前 </param>
    /// <returns> 停止する際に必要なIndex </returns>
    public int PlaySE(CueSheetType type, string cueName, float volume = 1f)
    {
        CriAtomEx.CueInfo cueInfo;
        CriPlayerData newAtomPlayer = new CriPlayerData();

        var cueSheetName = GetCueSheetName(type);
        if (cueSheetName == null){return -1;}
        var tempAcb = CriAtom.GetCueSheet(cueSheetName).acb;
        tempAcb.GetCueInfo(cueName, out cueInfo);

        newAtomPlayer.CueInfo = cueInfo;

        if (newAtomPlayer.IsLoop)
        {
            _loopSEPlayer.SetCue(tempAcb, cueName);
            _loopSEPlayer.SetVolume(volume * _seVolume * _masterVolume);
            newAtomPlayer.Playback = _loopSEPlayer.Start();
        }
        else
        {
            _sePlayer.SetCue(tempAcb, cueName);
            _sePlayer.SetVolume(volume * _seVolume * _masterVolume);
            newAtomPlayer.Playback = _sePlayer.Start();
        }

        _seData.Add(newAtomPlayer);
        return _seData.Count - 1;
    }

    /// <summary> SEをPauseさせる </summary>
    /// <param name="index">一時停止させたいPlaySE()の戻り値 (-1以下を渡すと処理を行わない)</param>
    public void PauseSE(int index)
    {
        if (index < 0) return;

        _seData[index].Playback.Pause();
    }

    /// <summary> PauseさせたSEを再開させる</summary>
    /// <param name="index">再開させたいPlaySE()の戻り値 (-1以下を渡すと処理を行わない)</param>
    public void ResumeSE(int index)
    {
        if (index < 0) return;

        _seData[index].Playback.Resume(CriAtomEx.ResumeMode.AllPlayback);
    }

    /// <summary> SEを停止させる </summary>
    /// <param name="index">止めたいPlaySE()の戻り値 (-1以下を渡すと処理を行わない)</param>
    public void StopSE(int index)
    {
        if (index < 0) return;

        _seData[index].Playback.Stop();
    }

    /// <summary> ループしているすべてのSEを止める </summary>
    public void StopLoopSE()
    {
        _loopSEPlayer.Stop();
    }

    /// <summary> Voiceを流す関数 </summary>
    /// <param name="type">流したいキューシートのタイプ</param>
    /// <param name="cueName">流したいキューの名前</param>
    /// <returns>停止する際に必要なIndex</returns>
    public int PlayVoice(CueSheetType type, string cueName, float volume = 1f)
    {
        CriAtomEx.CueInfo cueInfo;
        CriPlayerData newAtomPlayer = new CriPlayerData();

        var cueSheetName = GetCueSheetName(type);
        if (cueSheetName == null){return -1;}
        var tempAcb = CriAtom.GetCueSheet(cueSheetName).acb;
        tempAcb.GetCueInfo(cueName, out cueInfo);

        newAtomPlayer.CueInfo = cueInfo;

        _voicePlayer.SetCue(tempAcb, cueName);
        _voicePlayer.SetVolume(volume * _masterVolume * _voiceVolume);
        newAtomPlayer.Playback = _voicePlayer.Start();

        _voiceData.Add(newAtomPlayer);
        return _voiceData.Count - 1;
    }

    /// <summary> VoiceをPauseさせる </summary>
    /// <param name="index">一時停止させたいPlayVoice()の戻り値 (-1以下を渡すと処理を行わない)</param>
    public void PauseVoice(int index)
    {
        if (index < 0) return;

        _voiceData[index].Playback.Pause();
    }

    /// <summary> PauseさせたVoiceを再開させる </summary>
    /// <param name="index">再開させたいPlayVoice()の戻り値 (-1以下を渡すと処理を行わない)</param>
    public void ResumeVoice(int index)
    {
        if (index < 0) return;

        _voiceData[index].Playback.Resume(CriAtomEx.ResumeMode.AllPlayback);
    }

    /// <summary> Voiceを停止させる </summary>
    /// <param name="index">止めたいPlayVoice()の戻り値 (-1以下を渡すと処理を行わない)</param>
    public void StopVoice(int index)
    {
        if (index < 0) return;

        _voiceData[index].Playback.Stop();
    }

    private void Unload(Scene scene)
    {
        StopLoopSE();

        var removeIndex = new List<int>();
        for (int i = _seData.Count - 1; i >= 0; i--)
        {
            if (_seData[i].Playback.GetStatus() == CriAtomExPlayback.Status.Removed)
            {
                removeIndex.Add(i);
            }
        }

        foreach (var i in removeIndex)
        {
            _seData.RemoveAt(i);
        }
    }
}