# SimpleHttpServer for Unity

!!現在実験的に開発中!!
Unityアプリにシンプルなhttp APIサーバー機能を持たせるためのpackageです。

- Unity製アプリケーションにAPIサーバー機能を付加することで、Curl, Webブラウザ等からアクセスすることでアプリ操作・クエリできるようにします。
- Unityエディタ拡張によりAPIエンドポイント情報をOpenAPI.yml形式でエクスポートできるようにします。

## Concept

- Unityアプリの既存コードから簡素な手順でAPI公開
- APIを通して他アプリとの連動性を得る
- OpenAPI.ymlを通して既存のWeb開発圏のツール・手法を流用可能にする
- シンプル。APIサーバー機能のみを扱うことにし、静的ファイル、テンプレートエンジン他、多様な機能を持つ一般的なWebサーバーを目指さない。

```mermaid
graph LR

subgraph UnityApp
s
c
svr
end

s[[c#-routing-attribute]] -->|API仕様情報の付加| c[c# code] -->|export| o[OpenAPI.yml]
o -->|input| og[openapi-generator]
o -->|input| rd[redoc]

svr[[C#-api-server-script]] -.->|自動列挙| s

client -->|http| svr

subgraph ExternalTools
og
rd
end
```

## Installation

### dependencies

先に以下をimportしておいてください。

- com.cysharp.unitask
  - インストール方法は公式 https://github.com/Cysharp/UniTask.git を参照ください。

- com.unity.nuget.newtonsoft-json
  - Package Managerを開き、Install package by name... から com.unity.nuget.newtonsoft-json をインストールします。


### upm

本パッケージは Package Managerを開き、Install package from git URL... から以下をインストールします。

```
https://github.com/uisawara/usimplehttpserver.git?path=Assets/UnityPackages/mmzkworks.SimpleHttpServer
```

## コード例

### サーバーの起動・停止

```c#
public sealed class SimpleHttpServerBehaviour : MonoBehaviour
{
    [SerializeField] private int port = 8080;
    private SimpleHttpServer? server;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        // HttpServerの起動
        server = new SimpleHttpServer(port);
        // AttributeをもとにしたController登録 (ルーティング設定)
        server.RegisterControllersFrom(Assembly.GetExecutingAssembly());
        server.Start();
    }

    private void OnDestroy()
    {
        // HttpServerの停止
        server?.Stop();
        server = null;
    }
}
```

### URIルーティング設定

- Attribute設定によりルーティング定義を行います。
- URLパラメータはメソッド定義と紐づけされます。

```c#
[RoutePrefix("/api")]
public sealed class DemoController
{
    // GET /api/echo/hello?times=3 -> "hellohellohello"
    [HttpGet("/echo/{text}")]
    public string Echo(string text, int times = 1)
    {
        return string.Concat(Enumerable.Repeat(text, Math.Max(1, times)));
    }

    // GET /api/add/12/30 -> { "a":12, "b":30, "sum":42 }
    [HttpGet("/add/{a}/{b}")]
    public object Add(int a, int b)
    {
        return new { a, b, sum = a + b };
    }

    // POST /api/users  Body: {"name":"Alice","age":20}
    [HttpPost("/users")]
    public User CreateUser([FromBody] User req)
    {
        return new User { Id = Guid.NewGuid(), Name = req.Name, Age = req.Age };
    }

    // GET /api/users/{id}
    [HttpGet("/users/{id}")]
    public User GetUser(Guid id)
    {
        return new User { Id = id, Name = "Sample", Age = 42 };
    }
}

public sealed class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Age { get; set; }
}
```

## Sample code

upmにサンプルコードが付属しています。
Unity EditorでPackage Managerからインポートすることができます。

### ApplicationStateApiSample

- 幾つかのサンプルAPI実装が入っています。

| APIエンドポイント | レスポンス                                                   |
| ----------------- | ------------------------------------------------------------ |
| /api/echo/{text}  | textで指定されたテキストをエコーバックで返します。           |
| /api/state        | アプリ状態一式をJSON形式で取得します。                       |
| /api/state/{key}  | 種類を指定してアプリ状態をJSON形式で取得します。<br />keyには以下が使えます。<br />application: アプリ基本情報<br />environments: アプリの実行時環境変数、コマンドライン引数<br />runtime: 実行時情報 |

## APIドキュメント生成 (openapi.yml export)

APIドキュメントの作成を容易にするため OpenAPI yamlのエクスポートができます。

### 使いかた

- Unity EditorのMenuからTools/uSimpleHttpServer/Generate OpenAPI YAML を選択すると、openapi.ymlが出力されます。
- 一度実行すると Assets/Settings/OpenApiExportSettings が作成されます。
  - このファイルを編集することで出力設定を変えることができます。

## About AI Generation

- このリポジトリには生成AIからの出力コードが含まれています。
