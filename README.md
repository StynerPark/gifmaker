# gifmaker

Windows x64에서 동영상이나 화면 녹화로 프레임을 만들고 GIF, WebP 또는 MP4로 저장하는 프로그램입니다.

## 다운로드와 설치

최신 버전: [gifmaker v1.0.1](https://github.com/StynerPark/gifmaker/releases/tag/v1.0.1)

1. 릴리스에서 `gifmaker_v1.0.1_Setup.exe`를 내려받아 실행합니다. 기존 설치에는 `gifmaker_v1.0.1_patch.exe`를 사용할 수 있습니다.
2. 설치가 끝나면 시작 메뉴의 **gifmaker**를 실행합니다. 설치 중 바탕 화면 바로 가기도 선택할 수 있습니다.
3. 설치 파일에는 필요한 .NET 런타임, FFmpeg, VLC 구성 요소가 포함되어 있습니다. 별도 설치는 필요하지 않습니다.

기존 `D:\gif maker` 포터블 폴더는 설치본과 별개입니다. 기존 폴더를 지우지 않고 설치본을 확인할 수 있습니다.

## 빠른 사용법

1. 동영상을 **원본 영상** 영역으로 끌어다 놓습니다. 또는 **화면 녹화**에서 영역/전체 녹화를 시작합니다.
2. 동영상을 탐색하고 **시작점 지정**, **종료점 지정**을 누른 뒤 **+ 프레임 추출**을 누릅니다.
3. 가운데 **추출한 프레임**에서 프레임을 확인하고, 필요한 경우 지속시간을 바꾸거나 선택 삭제합니다.
4. 오른쪽 **결과 미리보기**를 확인합니다.
5. 필요한 편집을 적용한 뒤 **저장**에서 GIF, WebP, MP4 중 하나를 선택합니다.

원본 영상을 더블클릭하면 전체화면으로 전환됩니다. 전체화면 하단에 마우스를 대면 재생 이동바와 조작 버튼이 나타납니다. `Esc`는 전체화면 종료, `Space`는 재생/일시정지, `←`/`→`는 한 프레임 이동입니다. `[`와 `]`로 시작점과 끝점을 지정할 수 있으며, 전체화면 버튼 줄의 양끝에 지정 시간이 표시됩니다. 기본 화면과 전체화면의 재생 이동바에는 시작점·끝점을 빨간 반쪽 삼각형으로 표시합니다.

## 편집과 출력

| 기능 | 사용 방법 |
| --- | --- |
| 자르기 | **자르기**를 눌러 영역을 드래그하고 **적용**합니다. **초기화**로 자르기를 해제합니다. 자유 또는 고정 비율을 선택할 수 있습니다. |
| 이미지 보정 | **영역**에서 수정할 부분을 선택하고 편집한 전체 크기 이미지를 지정합니다. 원본 프레임과 수정 이미지의 픽셀 크기가 같아야 합니다. |
| 해상도 | 출력 해상도를 선택합니다. |
| 이미지 축소 확대 | 품질 슬라이더 아래의 `50%`, `100%`, `200%`를 누르거나 숫자 칸에 배율을 직접 입력합니다. 기본값은 `100`입니다. 입력값은 10~2000%입니다. 선택한 배율 버튼은 진하게 표시됩니다. 자르기 영역 선택과 별개로 출력 이미지의 픽셀 크기에 적용됩니다. |
| 워터마크 | **추가**에서 이미지 또는 텍스트 워터마크를 배치하고 **초기화**로 제거합니다. |
| 재생 순서 | **역방향 / Reverse**는 프레임 순서를 뒤집고, **왕복 / Ping-Pong**은 앞으로 재생한 뒤 거꾸로 재생합니다. 반복 횟수는 별도 설정입니다. |
| 저장 | **GIF**와 **WebP**는 애니메이션, **MP4**는 동영상으로 저장합니다. |
| tmp | 저장 영역 옆의 **tmp → 열기**를 누르면 현재 설치 폴더의 임시 작업 폴더가 열립니다. |

**캐시 비우기**는 임시 작업 파일을 정리합니다. 정상 종료할 때 현재 작업 세션 폴더도 삭제됩니다. 필요한 프레임은 먼저 저장하세요. 비정상 종료 후 남은 세션은 다음 실행 때 12시간 이상 지난 것부터 정리합니다.

## 화면 녹화와 색상

화면 녹화는 사용 가능한 캡처 방식을 선택합니다. GPU 캡처를 사용할 수 없으면 일반 화면 캡처로 전환합니다. VM을 포함한 모든 환경에서 동일한 색상이나 녹화 성능이 보장되는 것은 아니므로, HDR/SDR 화면에서는 짧은 녹화로 결과 색상을 확인하세요.

## 문제가 생길 때

- 녹화가 시작되지 않으면 대상 화면과 녹화 영역을 다시 선택하고 짧게 재시도하세요.
- 미리보기와 저장 결과가 다르면 저장 형식, 해상도, 이미지 축소 확대 값, 자르기 영역을 확인하세요.
- 이미지 보정이 적용되지 않으면 수정 이미지의 가로·세로 픽셀 크기가 원본 프레임과 같은지 확인하세요.
- 오류를 신고할 때 Windows 버전, HDR 사용 여부, GPU/VM 여부, 저장 형식, 재현 순서를 함께 적어 주세요.

## English quick guide

Install the [Windows x64 setup](https://github.com/StynerPark/gifmaker/releases/tag/v1.0.1). Drop a video into **Original Video**, set the start/end points, extract frames, edit the frame list, then export GIF, WebP, or MP4. Use **tmp → Open** beside the export buttons to open the temporary work folder. The current session folder is removed on normal exit. **Image scale** offers 50%, 100%, 200%, and a numeric percentage (10–2000%). It changes output pixel dimensions independently of crop. **Reverse** reverses frame order; **Ping-Pong** plays forward then backward. Screen recording falls back to software capture when GPU capture is unavailable.

## 소스와 빌드

이 소스는 현재 배포된 `gifmaker` 바이너리에서 복원한 것입니다. 예전 ClipForge 개발 저장소의 원본 XAML 소스와 동일하지 않습니다. WPF 리소스와 내부 어셈블리 이름 `ClipForgeWpf`는 기존 실행 파일과 호환되도록 유지했습니다.

Windows x64와 .NET 8 SDK에서 `dotnet build gifmaker.csproj -c Release`로 빌드할 수 있습니다. 빌드 결과 `ClipForgeWpf.dll`을 현재 포터블 배포 폴더의 `gifmaker.dll`로 복사하여 사용합니다. 설치 패키지는 `gifmaker.iss`로 빌드하며, 완전한 포터블 배포 폴더를 `dist\gifmaker`에 준비해야 합니다. 자세한 내용은 [BUILD_INFO.txt](BUILD_INFO.txt)를 보세요.

## License

프로젝트 소스의 라이선스는 [MIT](LICENSE)입니다. 배포본에 포함된 외부 라이브러리와 FFmpeg/VLC에는 각각의 라이선스가 적용됩니다.
