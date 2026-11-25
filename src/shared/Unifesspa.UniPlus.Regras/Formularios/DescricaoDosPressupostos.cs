namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// O que descreve os fatos que as regras de um formulário citam e ele não pergunta: os campos dos
/// outros formulários, que dão a apresentação do que eles perguntam; a apresentação do catálogo, para o
/// que nenhum formulário pergunta; e os agregados sobre os grupos, que com os derivados do sistema dizem
/// de que fatos cada fato calculado é calculado.
/// </summary>
public sealed record DescricaoDosPressupostos(
    IReadOnlyList<CampoApresentado> CamposDosOutrosFormularios,
    IReadOnlyDictionary<string, ApresentacaoDoFato> ApresentacaoDosNaoPerguntados,
    IReadOnlyList<DefinicaoAgregado> Agregados)
{
    /// <summary>Um pressuposto por código, na ordem dada.</summary>
    public IReadOnlyList<FatoPressuposto> Descrever(IReadOnlyList<string> codigos)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        Dictionary<string, CampoApresentado> perguntados = PerguntadosPorOutros();
        Dictionary<string, IReadOnlyList<string>> calculadosDe = CalculadosDe();

        return [.. codigos.Select(codigo => perguntados.TryGetValue(codigo, out CampoApresentado? campo)
            ? PerguntadoPor(campo)
            : DoCatalogo(codigo, calculadosDe))];
    }

    /// <summary>O primeiro campo que pergunta o fato é o que vale.</summary>
    private Dictionary<string, CampoApresentado> PerguntadosPorOutros()
    {
        Dictionary<string, CampoApresentado> perguntados = new(StringComparer.Ordinal);
        foreach (CampoApresentado campo in CamposDosOutrosFormularios)
        {
            perguntados.TryAdd(campo.FatoCodigo, campo);
        }

        return perguntados;
    }

    private Dictionary<string, IReadOnlyList<string>> CalculadosDe()
    {
        Dictionary<string, IReadOnlyList<string>> calculadosDe = new(DerivadosDoSistema.Dependencias, StringComparer.Ordinal);
        foreach (DefinicaoAgregado agregado in Agregados)
        {
            calculadosDe[agregado.Codigo] = [agregado.FatoDeMembro];
        }

        return calculadosDe;
    }

    private static FatoPressuposto PerguntadoPor(CampoApresentado campo) =>
        new(
            campo.FatoCodigo,
            campo.Apresentacao.Rotulo,
            campo.Apresentacao.TipoDeCampo,
            campo.Formato,
            campo.ValoresSelecionaveis,
            null);

    private FatoPressuposto DoCatalogo(string codigo, Dictionary<string, IReadOnlyList<string>> calculadosDe)
    {
        ApresentacaoDoFato? apresentacao = ApresentacaoDosNaoPerguntados.GetValueOrDefault(codigo);
        return new(codigo, apresentacao?.Rotulo, apresentacao?.TipoDeCampo, null, null, calculadosDe.GetValueOrDefault(codigo));
    }
}
