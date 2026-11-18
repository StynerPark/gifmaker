using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace ClipForgeWpf;

public sealed class UiLanguage : INotifyPropertyChanged
{
	private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
	{
		["Frame record"] = "프레임 녹화",
		["Video record"] = "영상 녹화",
		["Record screen and PC sound as MP4"] = "화면과 PC 재생 소리를 MP4로 녹화합니다",
		["Video recording · PC audio"] = "영상 녹화 중 · PC 소리 포함",
		["System audio capture did not stop."] = "PC 소리 녹음을 중지하지 못했습니다.",
		["System audio capture failed."] = "PC 소리 녹음에 실패했습니다.",
		["Audio merge timed out."] = "오디오 저장 시간이 초과되었습니다.",
		["Video recording · HDR → SDR"] = "영상 녹화 중 · HDR → SDR",
		["Frame recording · HDR → SDR"] = "프레임 녹화 중 · HDR → SDR",
		["Drag again to reselect · Right-click to cancel"] = "다시 드래그: 영역 재지정 · 우클릭: 취소",
		["Press Stop to finish · Right-click to cancel"] = "중지: 녹화 완료 · 우클릭: 취소",
		["Recording failed"] = "녹화 실패",
		["Recording did not start."] = "녹화를 시작하지 못했습니다.",
		["Recording file is empty."] = "녹화 파일이 비어 있습니다.",
		["Recording could not be finalized."] = "녹화 파일 저장을 완료하지 못했습니다.",
		["Could not attach the recording process to the application."] = "녹화 프로세스의 종료 관리를 설정하지 못했습니다.",
		["No capturable display was found."] = "캡처할 수 있는 모니터를 찾지 못했습니다.",
		["Select an area inside a display."] = "모니터 안쪽으로 영역을 지정하세요.",
		["Select displays connected to the same GPU for one recording."] = "서로 다른 GPU의 화면은 한 번에 녹화할 수 없습니다. 같은 GPU의 화면을 지정하세요.",
		["GIF Maker"] = "움짤생성기",
		["Original Video"] = "원본 영상",
		["Clear Cache"] = "캐시 비우기",
		["Drop a video file here"] = "여기에 동영상을 놓으세요",
		["Prev Frame"] = "이전 프레임",
		["Next Frame"] = "다음 프레임",
		["Play/Pause"] = "재생/일시정지",
		["Start"] = "시작",
		["End"] = "종료",
		["Set Start"] = "시작점 지정",
		["Set End"] = "종료점 지정",
		["+ Extract Frames"] = "+ 프레임 추출",
		["Stop"] = "중지",
		["Extracted Frames"] = "추출한 프레임",
		["Thumb"] = "미리보기",
		["Time"] = "시간",
		["Duration"] = "지속시간",
		["Select/Unselect All"] = "전체 선택/해제",
		["Change Duration"] = "지속시간 변경",
		["Delete Selected"] = "선택 삭제",
		["Delete All"] = "전체 삭제",
		["Result Preview"] = "결과 미리보기",
		["Preview Play/Pause"] = "미리보기 재생/일시정지",
		["Crop"] = "자르기",
		["Reset"] = "초기화",
		["Image Patch"] = "이미지 보정",
		["Area"] = "영역",
		["Resolution"] = "해상도",
		["Source"] = "원본",
		["Loop"] = "반복",
		["Quality"] = "품질",
		["Watermark"] = "워터마크",
		["Insert"] = "추가",
		["Live Recording"] = "화면 녹화",
		["Record Area"] = "영역 녹화",
		["● Full Record"] = "● 전체 녹화",
		["■ Stop"] = "■ 중지",
		["Playback Order"] = "재생 순서",
		["Output"] = "저장",
		["Image patch: off"] = "이미지 보정: 꺼짐",
		["Image patch: on"] = "이미지 보정: 켜짐",
		["Subtitle removal: choose a clean Ref"] = "이미지 보정: 기준 이미지 선택",
		["Muted"] = "음소거",
		["Mute"] = "음소거",
		["Sound"] = "소리 켜짐",
		["Crop Selection"] = "자르기 영역 선택",
		["Apply"] = "적용",
		["Free"] = "자유",
		["Cancel"] = "취소",
		["OK"] = "확인",
		["Insert Image/Text"] = "이미지/텍스트 추가",
		["Add Image"] = "이미지 추가",
		["Add Text"] = "텍스트 추가",
		["Opacity"] = "불투명도",
		["Font"] = "글꼴",
		["Size"] = "크기",
		["Color"] = "색상",
		["Double click text"] = "두 번 클릭하여 입력",
		["Duration (ms)"] = "지속시간(ms)",
		["Original"] = "원본",
		["Area Select"] = "영역 선택",
		["Reselect"] = "다시 선택",
		["● Record"] = "● 녹화",
		["Area · Freehand image patch"] = "Area · 자유 선택 이미지 패치",
		["Show original"] = "원본 보기",
		["Apply to all frames"] = "모든 프레임에 적용",
		["Load image…"] = "이미지 불러오기…",
		["Redraw selection"] = "영역 다시 그리기",
		["Feather(px)"] = "경계(px)",
		["Image import"] = "이미지 불러오기",
		["Patch failed"] = "패치 적용 실패",
		["Drop one edited image file."] = "수정 이미지 한 개를 드래그하세요.",
		["Draw around the area to replace. Release the mouse to close the selection."] = "교체할 부분을 마우스로 둘러 그리세요. 마우스를 놓으면 영역이 닫힙니다.",
		["Selection ready · Drop the edited full-frame image here."] = "영역 선택 완료 · 수정한 전체 프레임 이미지를 이 창에 드래그하세요.",
		["Preview · Only the selected area will be applied at the same position in every frame. Use Show original to compare."] = "합성 미리보기 · 선택 영역만 모든 프레임의 같은 위치에 적용됩니다. 원본 보기를 눌러 비교할 수 있습니다.",
		["① Draw a freehand selection.  ② Drop an edited full-frame image here.\n③ Check the preview and apply. Keep the original pixel dimensions; only matching coordinates are used."] = "① 마우스로 교체할 영역을 둘러 그리세요.  ② 로고를 지운 전체 프레임 이미지를 이 창에 드래그하세요.\n③ 합성 결과를 확인하고 적용하세요. 이미지는 원본과 같은 해상도여야 하며, 같은 좌표의 영역만 사용합니다.",
		["Draw a freehand selection and drop an edited full-frame image"] = "자유롭게 영역을 그리고 수정한 전체 프레임 이미지를 놓으세요",
		["Save a silent H.264 MP4 with the current edits and playback order"] = "현재 보정과 재생 순서를 반영한 무음 MP4 저장",
		["ON: save frames in reverse order. Click again to turn off."] = "켜짐: 역순으로 저장합니다. 다시 누르면 꺼집니다.",
		["ON: forward then reverse, without repeating the last frame (100 → 199)."] = "켜짐: 정방향 뒤 역방향을 추가합니다. 마지막 프레임 제외(100 → 199).",
		["Clear temporary files and the current workspace"] = "임시 파일과 현재 작업 내용을 비웁니다",
		["Switch interface language (KO / EN)"] = "화면 언어 전환(KO / EN)"
	};

	private static readonly Dictionary<string, string> StatusPhrases = new Dictionary<string, string>
	{
		["idle"] = "대기",
		["clearing cache"] = "캐시 정리 중",
		["cache cleared"] = "캐시 정리 완료",
		["reading original info"] = "원본 정보 읽는 중",
		["failed to read source FPS"] = "원본 FPS 확인 실패",
		["load failed"] = "불러오기 실패",
		["extraction is already running"] = "추출이 이미 진행 중입니다",
		["no source FPS info"] = "원본 FPS 정보가 없습니다",
		["choose an end after the start before extracting"] = "시작점보다 뒤에 종료점을 지정하세요",
		["extracting original frames"] = "원본 프레임 추출 중",
		["extraction stopped"] = "추출 중지됨",
		["extract failed"] = "추출 실패",
		["stopping extraction"] = "추출 중지 중",
		["stop recording before changing area"] = "영역 변경 전에 녹화를 중지하세요",
		["record area"] = "녹화 영역",
		["recording stopped, no frames"] = "녹화 중지됨: 프레임 없음",
		["recording stopped"] = "녹화 중지됨",
		["recording added"] = "녹화 프레임 추가",
		["recording failed"] = "녹화 실패",
		["building frame list"] = "프레임 목록 생성 중",
		["loading thumbnails"] = "미리보기 불러오는 중",
		["restoring subtitle preview"] = "이미지 보정 미리보기 생성 중",
		["image patch preview ready"] = "이미지 보정 미리보기 완료",
		["preview failed"] = "미리보기 실패",
		["playback order"] = "재생 순서",
		["Forward"] = "정방향",
		["ZIP saved"] = "ZIP 저장 완료",
		["preparing export"] = "저장 준비 중",
		["save failed"] = "저장 실패",
		["saved"] = "저장 완료",
		["applying image patch"] = "이미지 보정 적용 중",
		["rendering frames"] = "프레임 처리 중",
		["extracting"] = "추출 중",
		["recording"] = "녹화 중",
		["restoring"] = "보정 중",
		["rendering"] = "처리 중",
		["encoding"] = "인코딩 중",
		["crop reset"] = "자르기 초기화됨",
		["extract frames before selecting a patch area"] = "프레임을 추출한 뒤 보정 영역을 지정하세요",
		["subtitle removal reset"] = "이미지 보정 초기화됨",
		["insert overlays reset"] = "워터마크 초기화됨",
		["done"] = "완료",
		["frames"] = "프레임"
	};

	public static UiLanguage Current { get; } = new UiLanguage();

	public static bool Korean { get; private set; } = true;

	private static string SettingsPath => Path.Combine(AppContext.BaseDirectory, "gifmaker.language");

	public string this[string key] => T(key);

	public event PropertyChangedEventHandler PropertyChanged;

	public static void Load()
	{
		try
		{
			Korean = !File.Exists(SettingsPath) || File.ReadAllText(SettingsPath).Trim() != "EN";
		}
		catch
		{
			Korean = true;
		}
	}

	public static void Set(bool korean, bool save = true)
	{
		Korean = korean;
		if (save)
		{
			try
			{
				File.WriteAllText(SettingsPath, korean ? "KO" : "EN");
			}
			catch
			{
			}
		}
		Current.PropertyChanged?.Invoke(Current, new PropertyChangedEventArgs("Item[]"));
	}

	public static string T(string text)
	{
		if (text == null)
		{
			return null;
		}
		if (Korean && Labels.TryGetValue(text, out var value))
		{
			return value;
		}
		if (!Korean)
		{
			foreach (KeyValuePair<string, string> label in Labels)
			{
				if (label.Value == text)
				{
					return label.Key;
				}
			}
			return text;
		}
		if (text.StartsWith("Status: ", StringComparison.Ordinal))
		{
			string text2 = text.Substring(8);
			foreach (KeyValuePair<string, string> pair in StatusPhrases.OrderByDescending((KeyValuePair<string, string> p) => p.Key.Length))
			{
				text2 = Regex.Replace(text2, "(?<![A-Za-z])" + Regex.Escape(pair.Key) + "(?![A-Za-z])", (Match _) => pair.Value);
			}
			return "상태: " + text2;
		}
		if (text.StartsWith("Segment: "))
		{
			return "구간: " + text.Substring(9);
		}
		if (text.StartsWith("Source: "))
		{
			return text.Replace("Source:", "원본:").Replace("Original duration:", "원본 지속시간:");
		}
		return text;
	}

	public static void TranslateDialog(DependencyObject root)
	{
		if (root is Canvas)
		{
			return;
		}
		if (root is Window window)
		{
			window.Title = T(window.Title);
		}
		if (root is TextBlock textBlock && (Labels.ContainsKey(textBlock.Text) || textBlock.Text.StartsWith("Source: ")))
		{
			textBlock.Text = T(textBlock.Text);
		}
		if (root is TextBlock textBlock2 && !Korean && Labels.ContainsValue(textBlock2.Text))
		{
			textBlock2.Text = T(textBlock2.Text);
		}
		if (root is ContentControl { Content: string content } contentControl)
		{
			contentControl.Content = T(content);
		}
		foreach (object child in LogicalTreeHelper.GetChildren(root))
		{
			if (child is DependencyObject root2)
			{
				TranslateDialog(root2);
			}
		}
	}
}
