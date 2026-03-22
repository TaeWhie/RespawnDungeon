# **게임 로그 데이터 기반 생성형 롤플레이 챗봇 시스템 아키텍처 및 구현 전략 연구 보고서**

본 보고서는 게임 텔레메트리 로그를 활용하여 고도로 정교한 생성형 롤플레이(Roleplay, 이하 RP) 챗봇을 구축하기 위한 아키텍처 프레임워크와 데이터 엔지니어링 방법론을 분석한다. 최근 거대언어모델(LLM)의 비약적인 발전으로 인해 과거의 정적인 상태 머신(State Machine) 기반 NPC(Non-Player Character)는 점차 역동적이고 맥락 이해가 가능한 인공지능 에이전트로 대체되고 있다.1 이러한 시스템의 핵심은 게임 엔진에서 생성되는 방대한 로그 데이터를 언어 모델이 이해할 수 있는 지식 구조로 변환하고, 이를 장기 기억 시스템과 결합하여 일관성 있는 서사를 유지하는 데 있다.3 본 분석에서는 프롬프트 엔지니어링, 검색 증강 생성(RAG), 미세 조정(Fine-tuning)의 결합 모델을 제시하고, 게임 로그를 자연어 서사로 변환하는 파이프라인의 구체적인 설계를 논의한다.

## **생성형 롤플레이 시스템을 위한 최적화 방법론의 비교 분석**

성공적인 RP 챗봇 아키텍처를 설계하기 위해서는 프롬프트 엔지니어링, RAG, 미세 조정이라는 세 가지 주요 최적화 기법의 장단점을 명확히 이해하고 이를 유기적으로 통합해야 한다.5 각 기법은 모델의 출력 품질, 도메인 특화 정확도, 형식 준수 능력을 강화하는 데 서로 다른 역할을 수행한다.5

### **주요 최적화 기법의 기술적 특성 비교**

| 평가 기준 | 프롬프트 엔지니어링 (Prompt Engineering) | 검색 증강 생성 (RAG) | 미세 조정 (Fine-tuning) |
| :---- | :---- | :---- | :---- |
| **구현 난이도** | 낮음: 모델 파라미터 변경 없이 명시적 지시문 설계.5 | 중간: 벡터 데이터베이스 및 검색 파이프라인 구축 필요.6 | 높음: 대규모 데이터 준비 및 고성능 연산 자원 요구.6 |
| **운영 비용** | 낮음: 표준 API 호출 비용 내에서 관리 가능.6 | 중간: 임베딩 모델 및 데이터베이스 유지 관리 비용 발생.7 | 높음: GPU 자원 투자 및 전문 인력 운용 비용 상당.7 |
| **정확도 및 사실성** | 가변적: 프롬프트의 명확성과 길이 제한에 의존.6 | 높음: 실시간 외부 데이터 및 검증된 소스 기반 응답.5 | 높음: 모델 가중치에 특정 도메인 지식 내재화.5 |
| **지식 갱신 주기** | 즉시: 텍스트 지시문 수정으로 즉각 반영.6 | 실시간: 데이터베이스 인덱싱 업데이트로 상시 반영.6 | 지연됨: 재학습 주기에 따라 지식 업데이트 지연.6 |

프롬프트 엔지니어링은 모델에게 페르소나, 말투, 상황 설정을 부여하는 가장 기본적인 층위이다.6 하지만 모델의 문맥 창(Context Window) 한계로 인해 장기적인 대화 흐름을 유지하는 데는 한계가 있으며, 소위 '디지털 건망증' 현상이 발생하기 쉽다.11 RAG는 이러한 한계를 극복하기 위해 게임 로그나 설정집(Lorebook)을 벡터 데이터베이스에 저장하고, 대화 문맥에 따라 관련 정보를 동적으로 인출하여 프롬프트에 주입한다.5 이는 모델이 실시간 게임 상태 변화를 인식하게 하는 데 결정적인 역할을 한다.2 마지막으로 미세 조정은 특정 게임 세계관 고유의 문체, 슬랭, 행동 양식을 모델의 신경망 가중치에 각인시킨다.7 특히 고유한 성격적 결함이나 복잡한 내적 동기를 가진 캐릭터를 구현할 때 미세 조정은 프롬프트만으로는 도달하기 어려운 깊이 있는 재현을 가능케 한다.7

## **게임 로그 데이터 엔지니어링 및 전처리 파이프라인**

게임 로그는 대개 타임스탬프, 이벤트 코드, 좌표 데이터 등 기계 가독성 위주의 반정형 구조로 이루어져 있다.1 이를 LLM 학습 및 추론에 활용하기 위해서는 자연어 기반의 서사 구조로 변환하는 정교한 전처리 과정이 필수적이다.1

### **데이터 구조 표준화: Alpaca와 ShareGPT 포맷**

LLM 학습을 위해 로그를 구조화할 때 주로 사용되는 포맷은 Alpaca와 ShareGPT로 나뉜다.17 단순한 질의응답 시스템이 아닌 다회차 대화(Multi-turn Dialogue)가 핵심인 RP 시스템에서는 ShareGPT 포맷이 압도적으로 유리하다.17

| 특징 | Alpaca 포맷 | ShareGPT 포맷 |
| :---- | :---- | :---- |
| **구조적 형태** | 평면적 JSON 리스트 (instruction, input, output).17 | 계층적 구조 (conversations 내 순차적 턴).17 |
| **최적 용도** | 단순 명령 이행, 요약, 번역 작업.17 | 챗봇, 에이전트, 복잡한 문맥의 롤플레이 학습.17 |
| **턴 관리** | 단일 턴 중심의 데이터 쌍 구성.17 | 홀수/짝수 위치에 따른 화자 역할 엄격 관리.17 |

게임 로그를 ShareGPT 형식으로 변환할 때는 각 세션을 '티켓' 단위로 그룹화하는 로직을 적용해야 한다.17 예를 들어 플레이어의 행동 로그를 human 역할로, NPC의 반응 로그를 gpt 역할로 매핑하며, 행동(Action) 토큰은 특수 구분자(예: \*행동\*)를 사용하여 대화와 분리해야 한다.19 이때 from 태그는 학습 과정에서 특수 토큰으로 변환되어 모델이 각 턴의 역할을 인식하게 돕는다.17

### **로그 파싱 및 서사화 전략**

전통적인 정규표현식(Regex) 기반 파싱은 로그의 구문적 구조를 파악하는 데는 유용하지만 의미적 맥락을 놓치기 쉽다.1 최신 아키텍처에서는 LLM 자체를 로그 해석기로 활용하는 하이브리드 접근법이 권장된다.1 구체적인 파싱 절차는 다음과 같다.

1. **로그 수집 및 정규화**: Fluentd나 Logstash와 같은 도구를 통해 분산된 로그를 중앙 집중화하고 타임스탬프와 인코딩 형식을 통일한다.16  
2. **의미적 추출**: 로그 라인에 담긴 의미를 파악한다. 예를 들어 \<Player\> dealt 30 damage to \<Cow\>라는 로그를 "플레이어가 소를 공격하여 심한 상처를 입혔음"이라는 서사적 문구로 변환한다.2  
3. **청킹 및 요약**: 방대한 양의 로그를 모델의 문맥창 내에서 처리할 수 있도록 500\~1000자 단위로 분할(Chunking)하고, 중요도가 낮은 로그(예: 단순 이동 기록)는 제거하거나 압축한다.20  
4. **벡터 인덱싱**: 서사화된 로그 조각들을 벡터 임베딩 모델(예: nomic-embed-text)을 통해 수치화하여 벡터 데이터베이스에 저장한다.10

이 과정에서 "벡터 요약(Vector Summarization)" 기법을 적용하면 원본 로그의 노이즈를 제거하고 검색 정확도를 크게 높일 수 있다.22 이는 검색 시스템이 단순한 키워드 매칭을 넘어 사건의 인과관계와 서사적 중요도를 인식하게 하는 핵심 기제다.22

## **계층적 메모리 시스템 설계: 디지털 건망증 해결**

RP 챗봇의 가장 큰 기술적 난제는 장기적인 일관성 유지이다.11 모델의 자기주의(Self-attention) 메커니즘은 문맥이 길어질수록 정보의 영향력이 희석되는 '문맥 희석' 현상을 겪기 때문에, 인간의 기억 구조를 모사한 계층적 메모리 아키텍처가 필요하다.23

### **3단계 메모리 계층 구조**

1. **작동 기억 (Working Memory)**: 현재 진행 중인 장면의 페르소나 설정, 시나리오, 그리고 가장 최근 5\~10회차의 대화 내용을 실시간 RAM에 유지한다.26 이는 즉각적인 반응 속도를 보장한다.  
2. **단기 기억 (Episodic Buffer)**: 최근 몇 시간 내에 발생한 주요 사건들을 '롤링 요약(Rolling Summary)' 형태로 관리한다.26 새로운 사건이 발생할 때마다 기존 요약문과 결합하여 업데이트하며, 낡은 정보는 자연스럽게 장기 기억으로 전이되거나 폐기된다.3  
3. **장기 기억 (Archival Memory)**: 캐릭터의 배경 이야기, 플레이어와의 오랜 관계 변화, 세계관의 역사적 사실 등을 벡터 데이터베이스에 영구 보존한다.3 RAG 시스템은 사용자 쿼리와 유사도가 높은 기억 조각들을 호출하여 프롬프트의 지식 기반을 보강한다.10

### **동적 세계 상태 추적**

단순한 텍스트 대화 기록 외에도 수치화된 '세계 상태(World State)'를 추적하는 구조를 병행해야 한다.29

* **물리적 상태**: 캐릭터의 HP, 마나, 부상 상태, 보유 아이템 목록.29  
* **사회적 관계**: 팩션(Faction)에 대한 평판, 플레이어와의 호감도 및 신뢰 수치.29  
* **환경 메타데이터**: 시간 흐름, 날씨 변화, 현재 위치한 장소의 분위기 정보.29

이러한 수치 데이터는 매 턴마다 업데이트되어 프롬프트 내의 {SITUATION} 태그 등에 주입되며, 이는 LLM이 물리적 법칙을 무시하거나 설정 오류를 범하는 것을 방지하는 강력한 가이드라인이 된다.30

## **실전 구축을 위한 SillyTavern 캐릭터 카드 및 설정집 활용**

커뮤니티 표준으로 자리 잡은 SillyTavern 프론트엔드는 캐릭터 카드 V2 규격을 통해 정교한 RP 환경을 제공한다.33 게임 로그를 이 구조에 효과적으로 매핑하는 것이 실무적 핵심이다.

### **캐릭터 카드 V2 주요 필드 설계**

| 필드 명 | 기술적 역할 | 게임 로그 활용 방안 |
| :---- | :---- | :---- |
| name | 캐릭터 식별자.34 | 로그 내 화자 ID와 일치시켜 추적 자동화. |
| description | 항시 활성화되는 문맥 정보.34 | 로그에서 추출된 성격적 특징과 외형 묘사 배치. |
| personality | 캐릭터 보이스 및 행동 양식 지침.34 | 특정 상황(예: 부상 시)에서의 고유 반응 로직 기술. |
| scenario | 현재 장면의 제약 조건.34 | 게임 월드의 현재 맵 정보와 퀘스트 진행도 동적 주입. |
| lorebook | 키워드 기반 조건부 검색 지식.33 | 장소 이름, 전설 아이템 등 방대한 로그 지식의 효율적 관리. |

설정집(Lorebook/World Info)은 토큰 효율성의 핵심이다.36 모든 정보를 description 필드에 넣으면 토큰 소모가 극심해지고 모델의 주의력이 분산된다.36 대신 관련 키워드(예: 특정 NPC 이름이나 지명)가 대화에 등장할 때만 해당 정보를 프롬프트에 주입하는 "조건부 인출" 전략을 사용해야 한다.35

### **정규표현식(Regex)을 이용한 이벤트 트리거**

SillyTavern의 Regex 확장을 사용하면 게임 로그 내의 특정 패턴을 감지하여 자동 반응을 생성할 수 있다.35 예를 들어 로그에 EVENT\_QUEST\_COMPLETE 토큰이 나타나면 이를 감지하여 NPC가 플레이어에게 축하의 말을 건네도록 강제하거나, 특정 단어의 출력 형식을 변경(예: 속마음을 *기울임꼴*로 표시)할 수 있다.37

## **모델 선택 및 미세 조정 전략: 한국어 RP 특화 모델**

모델의 지능과 문체는 RP 챗봇의 품질을 결정짓는 가장 큰 변수이다.39 특히 한국어 사용자의 경우, 한국어 특유의 높임말 체계와 문화적 맥락을 이해하는 모델 선택이 중요하다.41

### **주요 모델 계열 분석**

| 모델 계열 | 강점 및 특징 | RP 적합성 분석 |
| :---- | :---- | :---- |
| **Llama 3 (Meta)** | 탁월한 성능과 방대한 생태계 지원.39 | 범용 성능은 높으나 한국어 뉘앙스 처리에는 추가 튜닝 필요.41 |
| **Mistral / Mixtral** | 효율적인 MoE 구조 및 긴 문맥창(최대 128k).40 | 대규모 세계관 기반의 긴 대화 세션에 유리.39 |
| **Bllossom (한국어 특화)** | Llama 3 기반 15조 토큰 학습으로 한국어 능통.41 | 반말/존댓말 구분 및 한국적 정서 재현에 최적화.41 |
| **Hermes / Noromaid** | 커뮤니티 제작 RP 특화 튜닝 모델.44 | 창의적 표현력과 페르소나 유지력이 매우 높음.45 |

### **QLoRA와 Unsloth를 이용한 저비용 고효율 미세 조정**

개인 개발자나 중소 규모 팀이 게임 로그를 기반으로 모델을 학습시킬 때 "Unsloth" 라이브러리는 필수적인 도구이다.14 Unsloth는 표준 트랜스포머 라이브러리 대비 메모리 사용량을 70% 절감하고 학습 속도를 2\~5배 가속화한다.47

1. **환경 구성**: Google Colab이나 T4급 이상의 GPU 환경에서 Unsloth를 설치한다.14  
2. **데이터 로딩**: 준비된 ShareGPT 형식의 JSONL 파일을 데이터셋 객체로 변환한다.14  
3. **어댑터 학습**: 전체 파라미터가 아닌 일부 가중치(LoRA 어댑터)만 학습시켜 VRAM 요구사항을 8\~16GB 수준으로 낮춘다.14  
4. **양자화 및 배포**: 학습된 어댑터를 기본 모델과 병합한 뒤, GGUF 형식으로 4-bit 또는 5-bit 양자화를 수행하여 일반 소비자용 하드웨어에서도 추론이 가능하도록 한다.48

## **게임 엔진(Unity/Unreal)과의 통합 아키텍처**

생성형 RP 챗봇을 실제 게임 내에서 구동하기 위해서는 게임 엔진과 LLM 백엔드 사이의 통신 브릿지가 필요하다.50

### **하이브리드 인지 구조 설계**

LLM이 모든 행동을 결정하게 하면 게임의 결정론적 로직이 무너질 수 있다.30 따라서 LLM은 '조언자' 혹은 '서사 생성기' 역할을 수행하고, 실제 최종 행동은 게임 엔진의 물리적 제약 조건 내에서 결정되는 하이브리드 방식이 권장된다.30

1. **요청 게이팅(Request Gating)**: 플레이어의 입력이나 로그 발생 시 무조건 LLM을 호출하지 않고, 중요한 결정 지점(Decision Point)에서만 호출하여 API 비용과 지연 시간을 관리한다.30  
2. **프롬프트 템플릿 주입**: 게임 상태 정보를 포함한 프롬프트를 백엔드 API(예: vLLM, Oobabooga)로 전송한다.50  
3. **출력 검증(Referee System)**: LLM이 제안한 행동이 게임 내에서 실행 가능한지(예: 죽은 캐릭터가 말을 하는지) 검증한 후, 텍스트는 UI에 출력하고 행동 코드는 캐릭터 컨트롤러에 전달한다.30

지연 시간(Latency) 문제를 해결하기 위해 응답의 첫 단어가 생성되자마자 즉시 스트리밍(Streaming)하는 방식을 도입해야 하며, "Interleaved Reasoning" 기법을 통해 모델이 사고하는 과정과 대답하는 과정을 겹쳐 수행함으로써 체감 응답 속도를 80% 이상 단축할 수 있다.54

## **정량적 평가 및 시스템 고도화 방안**

구축된 RP 챗봇이 일관성을 유지하는지 평가하기 위해서는 전통적인 언어 모델 벤치마크 외에 RP 특화 지표를 도입해야 한다.56

### **페르소나 보존 평가지표**

* **IOO (Intersection over Output)**: 모델의 출력이 사전에 정의된 페르소나 시연 데이터와 얼마나 유사한 단어 분포를 보이는지 측정한다.56  
* **IOR (Intersection over References)**: 모델이 추론 과정에서 RAG나 프롬프트에 제공된 참조 데이터를 얼마나 충실히 활용했는지 분석한다.56  
* **CharacterJudge**: 더 강력한 상위 모델(예: GPT-4o)을 심판으로 활용하여, 응답의 감정선, 기억의 정확도, 도덕적 일관성 등을 5점 척도로 평가한다.15

### **하드웨어 최적화 및 토큰 산술**

추론 성능 극대화를 위해 문맥 창의 활용도를 수학적으로 관리해야 한다. 다음 수식은 가용 문맥 자원을 계산하는 기본 공식이다.

![][image1]  
여기서 ![][image2]은 생성 가능한 토큰 여유분, ![][image3]는 모델의 총 문맥 창 크기, ![][image4]는 고정 페르소나 지침, ![][image5]은 인출된 메모리 조각, ![][image6]는 활성 대화 기록, ![][image7]는 동적 세계 상태 정보를 의미한다.11 개발자는 ![][image2]을 충분히 확보하기 위해 ![][image5]과 ![][image6]를 주기적으로 요약(Summarization)하고, 중요도가 낮은 정보는 설정집으로 전보(Transfer)하는 자동화 로직을 구축해야 한다.11

## **결론 및 제언**

게임 로그 데이터 기반의 RP 챗봇은 단순한 기술적 구현을 넘어, 가상 세계의 물리적 법칙과 인공지능의 창의적 서사가 만나는 접점이다. 본 보고서에서 제시한 하이브리드 아키텍처—ShareGPT 기반의 데이터 표준화, 3계층 메모리 시스템, 그리고 게임 엔진과의 RESTful 브릿지 결합—은 캐릭터의 생동감을 극대화하면서도 운영의 실효성을 담보할 수 있는 가장 진보된 설계 방향이다.3 특히 한국어 환경에서는 Bllossom과 같은 현지화 모델과 Unsloth를 통한 미세 조정을 결합함으로써, 거대 기업의 API에 의존하지 않고도 독자적인 페르소나를 가진 에이전트를 성공적으로 구현할 수 있다.41 향후 연구는 모델이 물리적 인과관계를 스스로 학습하는 'World Model'의 통합과, 대규모 멀티플레이 환경에서의 동기화된 메모리 공유 시스템으로 확장되어야 할 것이다.58

#### **참고 자료**

1. System Log Parsing with Large Language Models: A Review \- arXiv.org, 3월 18, 2026에 액세스, [https://arxiv.org/html/2504.04877v2](https://arxiv.org/html/2504.04877v2)  
2. AI NPCs that understand the game world through real-time logs : r/Unity3D \- Reddit, 3월 18, 2026에 액세스, [https://www.reddit.com/r/Unity3D/comments/1jbqr2a/ai\_npcs\_that\_understand\_the\_game\_world\_through/](https://www.reddit.com/r/Unity3D/comments/1jbqr2a/ai_npcs_that_understand_the_game_world_through/)  
3. How to Implement Long-Term Memory \- OneUptime, 3월 18, 2026에 액세스, [https://oneuptime.com/blog/post/2026-01-30-long-term-memory/view](https://oneuptime.com/blog/post/2026-01-30-long-term-memory/view)  
4. Automatic Bug Detection in LLM-Powered Text-Based Games Using LLMs \- arXiv, 3월 18, 2026에 액세스, [https://arxiv.org/html/2406.04482v1](https://arxiv.org/html/2406.04482v1)  
5. RAG vs fine-tuning vs. prompt engineering \- IBM, 3월 18, 2026에 액세스, [https://www.ibm.com/think/topics/rag-vs-fine-tuning-vs-prompt-engineering](https://www.ibm.com/think/topics/rag-vs-fine-tuning-vs-prompt-engineering)  
6. RAG vs Fine-tuning vs Prompt Engineering: Everything You Need to Know | InterSystems, 3월 18, 2026에 액세스, [https://www.intersystems.com/resources/rag-vs-fine-tuning-vs-prompt-engineering-everything-you-need-to-know/](https://www.intersystems.com/resources/rag-vs-fine-tuning-vs-prompt-engineering-everything-you-need-to-know/)  
7. RAG, Prompt Engineering, Fine Tuning: What's the Difference? \- New Horizons, 3월 18, 2026에 액세스, [https://www.newhorizons.com/resources/blog/rag-vs-prompt-engineering-vs-fine-funing](https://www.newhorizons.com/resources/blog/rag-vs-prompt-engineering-vs-fine-funing)  
8. RAG vs. Long-context LLMs \- SuperAnnotate, 3월 18, 2026에 액세스, [https://www.superannotate.com/blog/rag-vs-long-context-llms](https://www.superannotate.com/blog/rag-vs-long-context-llms)  
9. RAG vs Long-Context LLMs: A Comprehensive Comparison | by Rost Glukhov \- Medium, 3월 18, 2026에 액세스, [https://medium.com/@rosgluk/rag-vs-long-context-llms-a-comprehensive-comparison-9b30594c445e](https://medium.com/@rosgluk/rag-vs-long-context-llms-a-comprehensive-comparison-9b30594c445e)  
10. Building Smarter Chatbots: RAG and Vector Databases | by Yunus Kılıç | CodeX \- Medium, 3월 18, 2026에 액세스, [https://medium.com/codex/building-smarter-chatbots-rag-and-memory-with-vector-databases-1b41c947dc2f](https://medium.com/codex/building-smarter-chatbots-rag-and-memory-with-vector-databases-1b41c947dc2f)  
11. Run Epic Roleplaying Sessions with Local LLMs \- Arsturn, 3월 18, 2026에 액세스, [https://www.arsturn.com/blog/how-to-run-epic-roleplaying-sessions-with-local-llms](https://www.arsturn.com/blog/how-to-run-epic-roleplaying-sessions-with-local-llms)  
12. Evaluating LLM-Generated Versus Human-Authored Responses in Role-Play Dialogues \- ACL Anthology, 3월 18, 2026에 액세스, [https://aclanthology.org/2025.inlg-main.2.pdf](https://aclanthology.org/2025.inlg-main.2.pdf)  
13. RAG vs Long-Context LLMs: Approaches for Real-World Applications \- Prem AI, 3월 18, 2026에 액세스, [https://www.premai.io/blog/rag-vs-long-context-llms-approaches-for-real-world-applications](https://www.premai.io/blog/rag-vs-long-context-llms-approaches-for-real-world-applications)  
14. How to Fine-Tune a Local Mistral or Llama 3 Model on Your Own Dataset, 3월 18, 2026에 액세스, [https://machinelearningmastery.com/how-to-fine-tune-a-local-mistral-or-llama-3-model-on-your-own-dataset/](https://machinelearningmastery.com/how-to-fine-tune-a-local-mistral-or-llama-3-model-on-your-own-dataset/)  
15. Character-LLM: Role Simulation & Control \- Emergent Mind, 3월 18, 2026에 액세스, [https://www.emergentmind.com/topics/character-llm](https://www.emergentmind.com/topics/character-llm)  
16. How to Use LLMs for Log File Analysis: Examples, Workflows, and Best Practices | Splunk, 3월 18, 2026에 액세스, [https://www.splunk.com/en\_us/blog/learn/log-file-analysis-llms.html](https://www.splunk.com/en_us/blog/learn/log-file-analysis-llms.html)  
17. Dataset preparation for LLM post-training | Anyscale Docs, 3월 18, 2026에 액세스, [https://docs.anyscale.com/llm/fine-tuning/data-preparation](https://docs.anyscale.com/llm/fine-tuning/data-preparation)  
18. How to create a custom Alpaca instruction dataset for fine-tuning LLMs \- Zachary Proser, 3월 18, 2026에 액세스, [https://zackproser.com/blog/how-to-create-a-custom-alpaca-dataset](https://zackproser.com/blog/how-to-create-a-custom-alpaca-dataset)  
19. ParasiticRogue/Model-Tips-and-Tricks \- Hugging Face, 3월 18, 2026에 액세스, [https://huggingface.co/ParasiticRogue/Model-Tips-and-Tricks](https://huggingface.co/ParasiticRogue/Model-Tips-and-Tricks)  
20. LLM RAG Tutorial: Examples and Best Practices | LaunchDarkly, 3월 18, 2026에 액세스, [https://launchdarkly.com/blog/llm-rag-tutorial/](https://launchdarkly.com/blog/llm-rag-tutorial/)  
21. We Tried and Tested 10 Best Vector Databases for RAG Pipelines \- ZenML Blog, 3월 18, 2026에 액세스, [https://www.zenml.io/blog/vector-databases-for-rag](https://www.zenml.io/blog/vector-databases-for-rag)  
22. SillyTavern Vector Storage \- FAQ : r/SillyTavernAI \- Reddit, 3월 18, 2026에 액세스, [https://www.reddit.com/r/SillyTavernAI/comments/1oq2s8s/sillytavern\_vector\_storage\_faq/](https://www.reddit.com/r/SillyTavernAI/comments/1oq2s8s/sillytavern_vector_storage_faq/)  
23. Beyond Vector Databases: Architectures for True Long-Term AI Memory | by Abhishek Jain, 3월 18, 2026에 액세스, [https://vardhmanandroid2015.medium.com/beyond-vector-databases-architectures-for-true-long-term-ai-memory-0d4629d1a006](https://vardhmanandroid2015.medium.com/beyond-vector-databases-architectures-for-true-long-term-ai-memory-0d4629d1a006)  
24. How to Build AI Agents That Actually Remember: Memory Architecture for Production LLM Apps \- DEV Community, 3월 18, 2026에 액세스, [https://dev.to/pockit\_tools/how-to-build-ai-agents-that-actually-remember-memory-architecture-for-production-llm-apps-11fk](https://dev.to/pockit_tools/how-to-build-ai-agents-that-actually-remember-memory-architecture-for-production-llm-apps-11fk)  
25. Advanced Prompt Engineering: Theory, Practice, and Implementation \- Hugging Face, 3월 18, 2026에 액세스, [https://huggingface.co/blog/info5ec/advanced-prompt-engineering](https://huggingface.co/blog/info5ec/advanced-prompt-engineering)  
26. Caellwyn/long-memory-character-chat \- GitHub, 3월 18, 2026에 액세스, [https://github.com/Caellwyn/long-memory-character-chat](https://github.com/Caellwyn/long-memory-character-chat)  
27. AI Agent Memory Management \- When Markdown Files Are All You Need? \- Dev.to, 3월 18, 2026에 액세스, [https://dev.to/imaginex/ai-agent-memory-management-when-markdown-files-are-all-you-need-5ekk](https://dev.to/imaginex/ai-agent-memory-management-when-markdown-files-are-all-you-need-5ekk)  
28. Building a Memory-Efficient RAG Chatbot: New Long-Short-Term Memory Approach | by Amirmahdi aboutalebi | Medium, 3월 18, 2026에 액세스, [https://medium.com/@amirmahdi\_abtl/building-a-memory-efficient-rag-chatbot-new-long-short-term-memory-approach-c3364e21b117](https://medium.com/@amirmahdi_abtl/building-a-memory-efficient-rag-chatbot-new-long-short-term-memory-approach-c3364e21b117)  
29. I'm building a state-driven AI roleplay system — and I need opinion from aside : r/Chatbots, 3월 18, 2026에 액세스, [https://www.reddit.com/r/Chatbots/comments/1rji8h2/im\_building\_a\_statedriven\_ai\_roleplay\_system\_and/](https://www.reddit.com/r/Chatbots/comments/1rji8h2/im_building_a_statedriven_ai_roleplay_system_and/)  
30. Introducing Personica AI: A Cognitive NPC Brain for Unreal Engine \- UE Marketplace \- Epic Developer Community Forums, 3월 18, 2026에 액세스, [https://forums.unrealengine.com/t/introducing-personica-ai-a-cognitive-npc-brain-for-unreal-engine/2690129](https://forums.unrealengine.com/t/introducing-personica-ai-a-cognitive-npc-brain-for-unreal-engine/2690129)  
31. Sphiratrioth \- SX-2 Character Cards Environment (big improvement over the SX & SX-1 versions you may already know) : r/SillyTavernAI \- Reddit, 3월 18, 2026에 액세스, [https://www.reddit.com/r/SillyTavernAI/comments/1inhl5l/sphiratrioth\_sx2\_character\_cards\_environment\_big/](https://www.reddit.com/r/SillyTavernAI/comments/1inhl5l/sphiratrioth_sx2_character_cards_environment_big/)  
32. Character Cards from a Systems Architecture perspective : r/SillyTavernAI \- Reddit, 3월 18, 2026에 액세스, [https://www.reddit.com/r/SillyTavernAI/comments/1lwmadx/character\_cards\_from\_a\_systems\_architecture/](https://www.reddit.com/r/SillyTavernAI/comments/1lwmadx/character_cards_from_a_systems_architecture/)  
33. character-card-spec-v2/spec\_v2.md at main · malfoyslastname ..., 3월 18, 2026에 액세스, [https://github.com/malfoyslastname/character-card-spec-v2/blob/main/spec\_v2.md](https://github.com/malfoyslastname/character-card-spec-v2/blob/main/spec_v2.md)  
34. Character Design | docs.ST.app \- SillyTavern Documentation, 3월 18, 2026에 액세스, [https://docs.sillytavern.app/usage/core-concepts/characterdesign/](https://docs.sillytavern.app/usage/core-concepts/characterdesign/)  
35. World Info | docs.ST.app \- SillyTavern Documentation, 3월 18, 2026에 액세스, [https://docs.sillytavern.app/usage/core-concepts/worldinfo/](https://docs.sillytavern.app/usage/core-concepts/worldinfo/)  
36. How I create character cards in SillyTavern (and my own observations) \- Reddit, 3월 18, 2026에 액세스, [https://www.reddit.com/r/SillyTavernAI/comments/1qyoy9t/how\_i\_create\_character\_cards\_in\_sillytavern\_and/](https://www.reddit.com/r/SillyTavernAI/comments/1qyoy9t/how_i_create_character_cards_in_sillytavern_and/)  
37. Regex | docs.ST.app \- SillyTavern Documentation, 3월 18, 2026에 액세스, [https://docs.sillytavern.app/extensions/regex/](https://docs.sillytavern.app/extensions/regex/)  
38. Regex in Sillytavern for Lorebooks etc : r/SillyTavernAI \- Reddit, 3월 18, 2026에 액세스, [https://www.reddit.com/r/SillyTavernAI/comments/1qwsi6k/regex\_in\_sillytavern\_for\_lorebooks\_etc/](https://www.reddit.com/r/SillyTavernAI/comments/1qwsi6k/regex_in_sillytavern_for_lorebooks_etc/)  
39. Llama vs Mistral vs Phi: Complete Open-Source LLM Comparison for Enterprise (2026), 3월 18, 2026에 액세스, [https://blog.premai.io/llama-vs-mistral-vs-phi-complete-open-source-llm-comparison-for-enterprise-2026/](https://blog.premai.io/llama-vs-mistral-vs-phi-complete-open-source-llm-comparison-for-enterprise-2026/)  
40. The 11 best open-source LLMs for 2025 \- n8n Blog, 3월 18, 2026에 액세스, [https://blog.n8n.io/open-source-llm/](https://blog.n8n.io/open-source-llm/)  
41. Llama3 한국어 성능 테스트 | Colab에서 Meta-Llama-3 모델 사용해보기, 3월 18, 2026에 액세스, [https://littlefoxdiary.tistory.com/128](https://littlefoxdiary.tistory.com/128)  
42. Bllossom \- Hugging Face, 3월 18, 2026에 액세스, [https://huggingface.co/Bllossom](https://huggingface.co/Bllossom)  
43. The best open source large language model \- Baseten, 3월 18, 2026에 액세스, [https://www.baseten.co/blog/the-best-open-source-large-language-model/](https://www.baseten.co/blog/the-best-open-source-large-language-model/)  
44. Llama 3 8B Instruct vs Noromaid 20B (Comparative Analysis) \- Galaxy.ai Blog, 3월 18, 2026에 액세스, [https://blog.galaxy.ai/compare/llama-3-8b-instruct-vs-noromaid-20b](https://blog.galaxy.ai/compare/llama-3-8b-instruct-vs-noromaid-20b)  
45. NousResearch/Hermes-3-Llama-3.1-405B · Model Review (RP/ERP) \- Hugging Face, 3월 18, 2026에 액세스, [https://huggingface.co/NousResearch/Hermes-3-Llama-3.1-405B/discussions/4](https://huggingface.co/NousResearch/Hermes-3-Llama-3.1-405B/discussions/4)  
46. Model with similar capabilities as Fimbulvetr 11B v2, but with native 8K context size \- Reddit, 3월 18, 2026에 액세스, [https://www.reddit.com/r/LocalLLaMA/comments/1b0kitr/model\_with\_similar\_capabilities\_as\_fimbulvetr\_11b/](https://www.reddit.com/r/LocalLLaMA/comments/1b0kitr/model_with_similar_capabilities_as_fimbulvetr_11b/)  
47. Fine Tuning Open Source LLMs like Llama 3.1, Mistral and Gemma \- The Right Way\!, 3월 18, 2026에 액세스, [https://ripeseed.io/blogs/fine-tuning-open-source-llms-like-llama-3-1-mistral-and-gemma-the-right-way](https://ripeseed.io/blogs/fine-tuning-open-source-llms-like-llama-3-1-mistral-and-gemma-the-right-way)  
48. From Raw Chat Logs to a Local AI: An End-to-End Guide to Building a Personality Clone with Llama 3.1 and Unsloth | by pragnyanramtha | Medium, 3월 18, 2026에 액세스, [https://medium.com/@pragnyanramtha/from-raw-chat-logs-to-a-local-ai-an-end-to-end-guide-to-building-a-personality-clone-with-llama-3-1-b4a1d263b5e4](https://medium.com/@pragnyanramtha/from-raw-chat-logs-to-a-local-ai-an-end-to-end-guide-to-building-a-personality-clone-with-llama-3-1-b4a1d263b5e4)  
49. AI Identity Transfer: From Character.AI to Self-Hosted Infrastructure \- DEV Community, 3월 18, 2026에 액세스, [https://dev.to/toxy4ny/ai-identity-transfer-from-characterai-to-self-hosted-infrastructure-420a](https://dev.to/toxy4ny/ai-identity-transfer-from-characterai-to-self-hosted-infrastructure-420a)  
50. Self-hosted AI models | docs.ST.app \- SillyTavern Documentation, 3월 18, 2026에 액세스, [https://docs.sillytavern.app/usage/how-to-use-a-self-hosted-model/](https://docs.sillytavern.app/usage/how-to-use-a-self-hosted-model/)  
51. API Connections | docs.ST.app \- SillyTavern Documentation, 3월 18, 2026에 액세스, [https://docs.sillytavern.app/usage/api-connections/](https://docs.sillytavern.app/usage/api-connections/)  
52. Writing Case Studies Using Generative AI: Interactive Role Play \- Faculty Focus, 3월 18, 2026에 액세스, [https://www.facultyfocus.com/articles/teaching-with-technology-articles/writing-case-studies-using-generative-ai-interactive-role-play/](https://www.facultyfocus.com/articles/teaching-with-technology-articles/writing-case-studies-using-generative-ai-interactive-role-play/)  
53. \[Showcase\] Dialogue System and AI NPCs with local LLMs inside Unity : r/Unity3D \- Reddit, 3월 18, 2026에 액세스, [https://www.reddit.com/r/Unity3D/comments/1iczqt4/showcase\_dialogue\_system\_and\_ai\_npcs\_with\_local/](https://www.reddit.com/r/Unity3D/comments/1iczqt4/showcase_dialogue_system_and_ai_npcs_with_local/)  
54. SillyTavern \- AI/ML API Documentation, 3월 18, 2026에 액세스, [https://docs.aimlapi.com/integrations/sillytavern](https://docs.aimlapi.com/integrations/sillytavern)  
55. Interleaved Reasoning for Large Language Models via Reinforcement Learning, 3월 18, 2026에 액세스, [https://machinelearning.apple.com/research/interleaved-reasoning](https://machinelearning.apple.com/research/interleaved-reasoning)  
56. RAG-like Few-shot Learning for Large Language Model Role-playing \- arXiv.org, 3월 18, 2026에 액세스, [https://arxiv.org/html/2509.12168v1](https://arxiv.org/html/2509.12168v1)  
57. CoSER\\xspace: Coordinating LLM-Based Persona Simulation of Established Roles \- arXiv, 3월 18, 2026에 액세스, [https://arxiv.org/html/2502.09082v1](https://arxiv.org/html/2502.09082v1)  
58. Built an open source, self-hostable AI roleplay engine — looking for feedback\! \- Reddit, 3월 18, 2026에 액세스, [https://www.reddit.com/r/selfhosted/comments/1n7a1as/built\_an\_open\_source\_selfhostable\_ai\_roleplay/](https://www.reddit.com/r/selfhosted/comments/1n7a1as/built_an_open_source_selfhostable_ai_roleplay/)  
59. Osilly/Awesome-Interleaving-Reasoning \- GitHub, 3월 18, 2026에 액세스, [https://github.com/Osilly/Awesome-Interleaving-Reasoning](https://github.com/Osilly/Awesome-Interleaving-Reasoning)

[image1]: <data:image/png;base64,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>

[image2]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAA8AAAAYCAYAAAAlBadpAAABCUlEQVR4XmNgGAUDBxyB+DkQ/0fCr4D4FxD/BeKTQBwMxMwwDdjAHCD+DcQ2SGIgDWkMEEPKgJgRSQ4OeIH4MBDfBWJxNDlJIH6IQw4MNIH4LRCvAWIWNDlTIP4GxFeBWARNDgz8GCB+TUeXAIIGBohcMZo4HExiwPQvKxAnM0BcVArlYwAeID7AAAndY1D2dQaIbdOBWBimEBvA5l9QqFYyQELZFSqGFcD8W4QmbgzEXxkgUYgTYPMvCEQzQAxtRROHA3zxCzIUpLkcTRwOdID4PQNm/ILYqxhQNVcDsQuIYcsASTXo6RnkfxgApWdQgIEMiQXi2UDMiSRPEIC84ssACXGSNA4nAACtdjyTg8WIAAAAAABJRU5ErkJggg==>

[image3]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAA8AAAAYCAYAAAAlBadpAAABDklEQVR4Xu3TP0tCURjH8UdCMBJChDBQlGwp30EgSDQ0BrW7uuqkItHi2NDUElJtvoAImqIgh95BTUHoHtFQoH2f7tFOp+u9i6M/+MDhee7hcP5ckXlmmjhKOMAGFkx9CWkz/pdN9PCOLmq4xA0KuMbO5GuTKFr4RB2Lf9tSxBtexVlZJ57iC/t2w0oMV4aOJ6lghAYidsPJBZp2YR19PCNjN3xyJs5+j8RbtW0Xp2RZvC3+RK/jFkPxOcGwrOIFA6w5vdCMJysdB0UPdcsuJPAo4ZOT6GDFbRyLt+ddt2GiV6evzPf+s3jCHVJOT1/ZIaoScP85POAD5yjjBPfYloCJ4+gHOewZefn9k+Yh3wXlKf2EjsadAAAAAElFTkSuQmCC>

[image4]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAA8AAAAYCAYAAAAlBadpAAAA3ElEQVR4Xu3SvwsBYRzH8WcwGPzIJFFmZZJFMSizf8J/YPRXyCglg81qwaAsyt+gWCjCYpHC++48dfd1uEFZ7lOvuu7zPE/37Tml/PwvZWxxtzli93w+o4mI3uCWDq4oivc5ZR00Qkh0ZsKYYYm46IwNU9xQcVZWMjhggIDoYlgo968yU1XWfHVZkAIumCMqOjMt5X6ysXiCPfKiM6NnMk7vo/3UwwZdpPRiGT3vGGkkbIK2da7R8zZk4SXGvG+v4VP0/a6QdFbfk8UJQ+VhPp0S1ur1f67ZF/n5QR5MFzA3axM0vAAAAABJRU5ErkJggg==>

[image5]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABUAAAAYCAYAAAAVibZIAAABSElEQVR4Xu2TvytGYRTHjzJQLCgDw5vNKFn8KAMjI3+AxWzB9i4GJWU2ShYjJRn8E0ZlIJOUMJAfn+899+m9x5XeO6r7rU/3ds+55znP9zmPWa1aSZNwD185VzAQMqLm4cM8V88L6A8ZBe3AHdzC8I9Ykn4+gic4hs4YjuqBA9iDFxiP4UwdsAqb8AlrMVzWCOzDkvm2FmI405h50Q14h+kYLmvRvIMJeLVyF93QhCE4hWsYLCb8pibMwSg8wFaImi2bx1X0xir4qcPR6uri0NxDqQHr5kVUuJKfXeYLXOboXYVUsOGp2XslPyV1py6TZ+pMW5e0aGU/k+SnfJ2FbfNDkmSPZrgtPzXMsiBJfmkC9LPGKKltP6fgBHoL3zSjmlVZkg5L0g7+9HMGHq11399gJY/pNp1b6z7vwnOel3LPoC+P1/pP+gaEiEZwd75/rQAAAABJRU5ErkJggg==>

[image6]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABIAAAAYCAYAAAD3Va0xAAAA7ElEQVR4Xu3TMWoCURSF4RuwCEklppGswCqFrU3ABdhYSMgm0mQflhIICIEUtlaprF2EU1koppCkiUb9Tx6RmcswKBaDMAc+cN6Z4T0uT7Mi5597zLGN+cQjbjDCb6z7wjuu9XFaXrBB0xcW1tS94sJ1iZQxRoTbZPWXZwunefCFTw0LDFBynZ61rl7vZUY7aUft7KMTRhZOrJNnpos1Wqg6bQvz0TuZ+Z/PN/roORPLaz5PvrAj56P7s0LDFxbW1B08n8hOvD91C0NOm88lhljiznX76MpPLfn/mqGDCj7wE+v0+w1X+rhIDtkB6VxBxYmwCFUAAAAASUVORK5CYII=>

[image7]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAA0AAAAYCAYAAAAh8HdUAAABEklEQVR4Xu3SIUtDYRTG8TNUcCgTFJsWsQxsCrJmEpNFwYFrC4LBNBFlVUQwyAzLaxaTXcSoyWARBAd+AcPi1P+zs+G9hwv24QM/GOe8793ZuTP7z5+ZxBrWMdWvzWB2cCCZMdTxhgPs4wkXeEDx96hnFE1cYyJR1zc84t58glRKaGMpNsgJGrGonOIDc7FBDrEZi0oL3zjGSOgtYjrUeimbX5Iu7lBFIXkoRps7M78wuCzPlj1yKhpNqz3Hp/nFvdQJ80OaORcbZANfOIqNBVyZv6eYZXSwGxta5S3GY4NU8Ir52ND70dNWQ10jawlbod77W9yghpf+Z635Eu/YsYzfmjd/oqKVr2Db/B+eNe5Q5gewgSt4tYoknAAAAABJRU5ErkJggg==>