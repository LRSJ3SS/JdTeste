# UABEA Web — porte experimental (Blazor WebAssembly)

Este projeto é o esqueleto inicial do UABEA (UABEAvalonia, de nesrak1)
rodando no navegador. **Não foi compilado nem testado nesta sessão** —
o ambiente onde isso foi gerado não tem o SDK .NET instalado (e a rede
está restrita, sem acesso ao instalador da Microsoft). Vale rodar isso
localmente antes de assumir que compila de primeira.

## Como compilar e rodar

Requisitos: [.NET 8 SDK](https://dotnet.microsoft.com/download) instalado
(Linux ou Windows).

```bash
cd UABEA.Web
dotnet restore
dotnet run
```

Isso sobe um servidor de desenvolvimento local (normalmente
`https://localhost:5001` ou similar — o terminal mostra a URL exata).

Para publicar como arquivos estáticos (para hospedar em qualquer
servidor web, GitHub Pages, etc.):

```bash
dotnet publish -c Release -o publish
```

O resultado fica em `publish/wwwroot/`.

## O que já está aqui

- Estrutura básica de projeto Blazor WebAssembly (.csproj, Program.cs,
  App.razor, layout, CSS).
- `AssetWorkspaceService`: envolve o `AssetsManager` da mesma biblioteca
  **AssetsTools.NET** que o UABEA desktop usa — via NuGet, não copiado
  manualmente — para abrir arquivos `.assets` recebidos como `byte[]`
  (via `<InputFile>` do navegador) em vez de caminho de disco.
- `Pages/Home.razor`: tela única com upload de arquivo e uma tabela
  listando os objetos (Path ID, tipo, tamanho) do `.assets` aberto.
- `wwwroot/classdata.tpk`: copiado de `ReleaseFiles/classdata.tpk` do
  zip original — necessário para a lib conseguir nomear os tipos
  (GameObject, Transform, MonoBehaviour, etc).

## Pontos a validar ao compilar (não confirmados nesta sessão)

1. **Assinatura de `Manager.LoadClassPackage(Stream)`** — usei a API
   pública documentada da AssetsTools.NET, mas não tive como abrir a
   DLL para confirmar o nome exato do overload nesta versão do pacote
   (3.0.9). Se der erro de compilação nessa linha, veja no IntelliSense
   quais overloads `LoadClassPackage` tem disponíveis (deve aceitar
   `string`, `Stream` ou `byte[]`) e ajuste.
2. **Versão do pacote AssetsTools.NET** — fixei `3.0.9` no `.csproj`
   como estimativa razoável para .NET 8 / Avalonia 11 desta época; pode
   ser necessário ajustar para a versão exata que o projeto original
   referenciava (isso está resolvido via `Libs/AssetsTools.NET.dll` no
   zip original, não via NuGet — então a versão NuGet mais próxima pode
   ter pequenas diferenças de API).
3. **`AssetHelper.FindAssetClassByID`** — usado para achar o nome do
   tipo de cada asset; confirme que esse é o método certo na versão do
   pacote (pode ter mudado de nome entre versões da lib).

## O que NÃO está incluído (e por quê)

| Recurso do UABEA original | Motivo de estar fora |
|---|---|
| Edição/exportação de assets | Ainda não portado — próximo passo natural depois que a listagem básica estiver validada |
| Plugin de Texturas (ver/exportar imagens) | Decodificação depende de **TexToolWrap** (ispc, crunch, PVRTexLib) — bibliotecas **nativas C/C++**, incompatíveis com WASM sem recompilar para Emscripten. Fora do escopo deste porte inicial |
| Plugin de Áudio (AudioClipPlugin) | Mesma limitação de dependências nativas, a confirmar caso a caso |
| Abertura de bundles (.bundle/.unity3d) | Só `.assets` solto foi portado agora; bundles têm múltiplos arquivos internos e mais lógica de I/O a adaptar |
| Suporte a IL2CPP (LibCpp2IL) | Não incluído nesta primeira versão; pode ser adicionado depois via NuGet se necessário |
| Diálogos nativos de arquivo, menus, atalhos de teclado | Trocados pela API de upload do navegador (`<InputFile>`); UI é uma página simples, não uma réplica da janela do Avalonia |

## Próximos passos sugeridos

1. Compilar localmente e corrigir os pontos de API acima.
2. Testar com um `.assets` real pequeno e confirmar que a listagem bate
   com o que o UABEA desktop mostra para o mesmo arquivo.
3. Adicionar visualização de conteúdo de um asset específico (clicar
   numa linha da tabela e ver os campos, como no `InfoWindow` original).
4. Decidir se vale a pena investir em recompilar TexToolWrap para WASM
   (trabalho grande) ou aceitar texturas como fora do escopo web.
