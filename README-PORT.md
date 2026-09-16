# UABEA Web — porte experimental (Blazor WebAssembly)

Este projeto é o porte do UABEA (UABEAvalonia, de nesrak1) rodando no
navegador, incluindo agora visualização básica de texturas. **Não foi
compilado nem testado nesta sessão** — o ambiente onde isso foi gerado
não tem o SDK .NET instalado (e a rede está restrita, sem acesso ao
instalador da Microsoft). Vale rodar isso localmente e ajustar os
pontos abaixo antes de assumir que compila de primeira.

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
- `Pages/Home.razor`: tela com upload de arquivo, tabela listando os
  objetos (Path ID, tipo, tamanho) do `.assets` aberto, e um botão
  "Ver textura" em cada linha `Texture2D` (TypeId 28).
- `SimpleTextureDecoder`: decodificador de texturas em **C# puro**,
  sem nenhuma dependência nativa, cobrindo formatos não-comprimidos:
  RGBA32, RGB24, ARGB32, BGRA32, Alpha8, R8, RG16, RGB565, RGBA4444,
  ARGB4444, R16, RFloat/RGFloat/RGBAFloat, RHalf/RGHalf/RGBAHalf.
- `PngEncoder`: encoder PNG minimalista, também em C# puro (usa só
  `System.IO.Compression`, já embutido no runtime), para transformar o
  RGBA decodificado numa imagem exibível via `<img src="data:...">`.
- `wwwroot/classdata.tpk`: copiado de `ReleaseFiles/classdata.tpk` do
  zip original — necessário para a lib conseguir nomear os tipos
  (GameObject, Transform, MonoBehaviour, etc).

## Pontos a validar ao compilar (não confirmados nesta sessão)

1. **Assinatura de `Manager.LoadClassPackage(Stream)`** — usei a API
   pública documentada da AssetsTools.NET, mas não tive como abrir a
   DLL para confirmar o nome exato do overload nesta versão do pacote
   (3.0.5). Se der erro de compilação nessa linha, veja no IntelliSense
   quais overloads `LoadClassPackage` tem disponíveis (deve aceitar
   `string`, `Stream` ou `byte[]`) e ajuste.
2. **`baseField["image data"].AsByteArray`** — no UABEA original
   (`TexturePlugin/TextureHelper.cs`, método `GetByteArrayTexture`), o
   campo `"image data"` às vezes precisa ser forçado para
   `AssetValueType.ByteArray` diretamente no *template* antes de
   materializar o valor, dependendo da versão da engine do arquivo
   aberto. Aqui foi usado o caminho genérico (`GetBaseField` +
   indexador), que costuma funcionar, mas se `imageData` vier vazio
   para uma textura que claramente tem dados (`ByteSize` grande na
   listagem), esse é o primeiro lugar a revisar, comparando com o
   método original como referência.
3. **`baseField["m_Width"|"m_Height"|"m_TextureFormat"].AsInt`** — nomes
   de campo conferem com os usados no UABEA original e na wiki oficial
   da AssetsTools.NET, mas podem variar sutilmente conforme a versão
   do Unity do arquivo (raro, mas acontece com campos legados).
4. **Orientação vertical da imagem** — a Unity guarda texturas com a
   primeira linha na parte de baixo; o código já inverte isso
   (`FlipVertically`), mas se a imagem aparecer de cabeça para baixo
   em algum caso específico, esse é o lugar a revisar.

## O que NÃO está incluído (e por quê)

| Recurso do UABEA original | Motivo de estar fora |
|---|---|
| Texturas comprimidas (ETC/ETC2, ASTC, PVRTC, DXT/BC) | Decodificação depende de **TexToolWrap** (ispc, crunch, PVRTexLib) — bibliotecas **nativas C/C++**, incompatíveis com WASM sem recompilar para Emscripten. Alternativa viável sem lib nativa: reimplementar os algoritmos de decodificação em C#/JS puro (trabalho adicional, não feito ainda) |
| Texturas "streamed" (dados num `.resS` externo) | `m_StreamData` aponta para um arquivo externo que não foi carregado — ainda não suportado |
| Edição/exportação de assets | Ainda não portado — próximo passo natural depois que a visualização básica estiver validada |
| Plugin de Áudio (AudioClipPlugin) | Mesma limitação de dependências nativas, a confirmar caso a caso |
| Abertura de bundles (.bundle/.unity3d) | Só `.assets` solto foi portado agora; bundles têm múltiplos arquivos internos e mais lógica de I/O a adaptar |
| Suporte a IL2CPP (LibCpp2IL) | Não incluído nesta primeira versão; pode ser adicionado depois via NuGet se necessário |
| Diálogos nativos de arquivo, menus, atalhos de teclado | Trocados pela API de upload do navegador (`<InputFile>`); UI é uma página simples, não uma réplica da janela do Avalonia |

## Próximos passos sugeridos

1. Compilar localmente e corrigir os pontos de API acima.
2. Testar com um `.assets` real que tenha texturas RGBA32/RGB24 (as
   mais comuns em builds de desenvolvimento/PC) e confirmar que a
   imagem decodificada bate com o que o UABEA desktop mostra.
3. Se precisar de formatos comprimidos, avaliar implementar decoders
   de ETC1/ETC2 e BC1-BC7 em C# puro (existem referências públicas do
   algoritmo) antes de partir para recompilar as libs nativas via
   Emscripten, que é o caminho mais caro.
4. Adicionar exportação da textura decodificada como arquivo PNG
   (já temos os bytes prontos em `PngEncoder.Encode`, só falta o botão
   de download).
