using System.Text;

using Unifesspa.UniPlus.PermissionCatalogGenerator;

if (args.Length is not 2 and not 3)
{
    Console.Error.WriteLine("Uso: permission-catalog-generator [--verify] <permissions.yml> <arquivo-gerado.cs>");
    return 2;
}

bool verificar = args[0] == "--verify";
if (verificar && args.Length != 3)
{
    Console.Error.WriteLine("Uso: permission-catalog-generator --verify <permissions.yml> <arquivo-gerado.cs>");
    return 2;
}

if (!verificar && args.Length != 2)
{
    Console.Error.WriteLine("Uso: permission-catalog-generator <permissions.yml> <arquivo-gerado.cs>");
    return 2;
}

int indiceCaminhos = verificar ? 1 : 0;
string caminhoFonte = args[indiceCaminhos];
string caminhoSaida = args[indiceCaminhos + 1];
string conteudoGerado = CatalogoPermissoesGerador.GerarDeArquivo(caminhoFonte);

if (verificar)
{
    // Gerar já normaliza para LF; um checkout com core.autocrlf=true (comum
    // no Windows, sem .gitattributes fixando eol=lf neste repositório)
    // converteria o artefato versionado para CRLF — normaliza a leitura
    // também, para não reportar divergência por causa da própria quebra de
    // linha, e não do conteúdo.
    if (!File.Exists(caminhoSaida)
        || !string.Equals(
            File.ReadAllText(caminhoSaida).ReplaceLineEndings("\n"),
            conteudoGerado,
            StringComparison.Ordinal))
    {
        Console.Error.WriteLine($"O artefato gerado está divergente: {caminhoSaida}. Execute o gerador novamente.");
        return 1;
    }

    return 0;
}

string? diretorioSaida = Path.GetDirectoryName(caminhoSaida);
if (!string.IsNullOrWhiteSpace(diretorioSaida))
{
    Directory.CreateDirectory(diretorioSaida);
}

File.WriteAllText(caminhoSaida, conteudoGerado, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
return 0;
