namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Um grupo repetível do formulário congelado, com os fatos dos campos dele.</summary>
internal sealed record GrupoDoProcesso(
    string Codigo, string Rotulo, string? Secao, int Ordem, IReadOnlyDictionary<string, FatoDoProcesso> Subitens);

/// <summary>
/// Os fatos da inscrição como a versão do processo os congelou: os coletados (fora e dentro de grupo) e os derivados.
/// Lê só o envelope congelado, nunca o catálogo vivo (a Seleção congela por cópia na publicação).
/// </summary>
internal sealed record FatosDaVersao(
    IReadOnlyDictionary<string, FatoDoProcesso> Coletados,
    IReadOnlyList<GrupoDoProcesso> Grupos,
    IReadOnlyDictionary<string, FatoDoProcesso> Derivados)
{
    /// <summary>Extrai os fatos da finalidade Inscrição do envelope reidratado.</summary>
    public static FatosDaVersao De(EnvelopeReidratado envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        Dictionary<string, Dictionary<string, string>> doProcesso = [];
        foreach (OpcaoDeclaradaFato opcao in envelope.Grafo.OpcoesDeclaradas)
        {
            if (!doProcesso.TryGetValue(opcao.FatoCodigo, out Dictionary<string, string>? porCodigo))
            {
                doProcesso[opcao.FatoCodigo] = porCodigo = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            porCodigo[opcao.Codigo] = opcao.Rotulo;
        }

        FatoDoProcesso Coletado(FatoColetado fato) => new(
            fato.FatoCodigo,
            fato.Rotulo,
            fato.Descricao,
            TiposDeItem.De(fato.TipoRenderizacao),
            ComponentesDeItem.De(
                fato.ComponenteExibicao,
                EhCpf(fato.FatoCodigo, fato.Formato),
                Classificacao(fato.ClassificacaoProtecao) == ClassificacaoDeProtecao.Identificador),
            Classificacao(fato.ClassificacaoProtecao),
            string.IsNullOrWhiteSpace(fato.HipoteseLegal) ? null : fato.HipoteseLegal,
            fato.EtapaCodigo,
            fato.Ordem,
            Opcoes(fato.FatoCodigo, doProcesso, envelope),
            EhCpf(fato.FatoCodigo, fato.Formato));

        IEnumerable<FatoColetado> daInscricao = envelope.Grafo.FatosColetados.Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao);
        Dictionary<string, FatoDoProcesso> coletados = daInscricao
            .Where(static f => f.GrupoColetadoId is null)
            .GroupBy(static f => f.FatoCodigo, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, g => Coletado(g.First()), StringComparer.Ordinal);

        List<GrupoDoProcesso> grupos =
        [
            .. envelope.Grafo.GruposColetados
                .Where(static g => g.Finalidade == FinalidadeFormulario.Inscricao)
                .Select(g => new GrupoDoProcesso(
                    g.Codigo, g.Rotulo, g.EtapaCodigo, g.Ordem,
                    g.Subitens.GroupBy(static s => s.FatoCodigo, StringComparer.Ordinal)
                        .ToDictionary(static s => s.Key, s => Coletado(s.First()), StringComparer.Ordinal))),
        ];

        Dictionary<string, FatoDoProcesso> derivados = envelope.FatosDerivadosCongelados.ToDictionary(
            static d => d.Key,
            d => Derivado(d.Value, envelope),
            StringComparer.Ordinal);

        return new FatosDaVersao(coletados, grupos, derivados);
    }

    private static FatoDoProcesso Derivado(FatoDerivadoCongelado derivado, EnvelopeReidratado envelope) =>
        new(
            derivado.Codigo,
            derivado.Nome,
            derivado.Descricao,
            TiposDeItem.De(derivado.Dominio, derivado.Cardinalidade),
            ComponentesDeItem.De(derivado.ComponenteExibicao, formatoCpf: false, identificador: false),
            Classificacao(derivado.ClassificacaoProtecao),
            string.IsNullOrWhiteSpace(derivado.HipoteseLegal) ? null : derivado.HipoteseLegal,
            Secao: null,
            Ordem: 0,
            DescricoesDoDominio(derivado.Codigo, envelope),
            EhCpf: false);

    private static Dictionary<string, string> Opcoes(
        string fatoCodigo, Dictionary<string, Dictionary<string, string>> doProcesso, EnvelopeReidratado envelope)
    {
        Dictionary<string, string> opcoes = new(DescricoesDoDominio(fatoCodigo, envelope), StringComparer.Ordinal);
        if (doProcesso.TryGetValue(fatoCodigo, out Dictionary<string, string>? declaradas))
        {
            foreach ((string codigo, string rotulo) in declaradas)
            {
                opcoes[codigo] = rotulo;
            }
        }

        return opcoes;
    }

    private static Dictionary<string, string> DescricoesDoDominio(string fatoCodigo, EnvelopeReidratado envelope)
    {
        Dictionary<string, string> descricoes = new(StringComparer.Ordinal);
        if (envelope.ValoresSelecionaveisCongelados?.GetValueOrDefault(fatoCodigo) is { } valores)
        {
            foreach (ValorDominioDeclaradoCongelado valor in valores)
            {
                descricoes[valor.Codigo] = valor.Descricao ?? valor.Codigo;
            }
        }

        if (envelope.MetadadosFatosCongelados?.GetValueOrDefault(fatoCodigo)?.ValoresDominioDeclarados is { } declarados)
        {
            foreach (ValorDominioDeclaradoCongelado valor in declarados)
            {
                descricoes.TryAdd(valor.Codigo, valor.Descricao ?? valor.Codigo);
            }
        }

        return descricoes;
    }

    private static bool EhCpf(string fatoCodigo, string? formato) =>
        string.Equals(fatoCodigo, ConjuntoBasicoDaInscricao.Cpf, StringComparison.Ordinal)
        || string.Equals(formato, "CPF", StringComparison.Ordinal);

    private static ClassificacaoDeProtecao Classificacao(string? token) =>
        ClassificacoesDeProtecao.TryAnalisar(token, out ClassificacaoDeProtecao classificacao) ? classificacao : ClassificacaoDeProtecao.Nenhuma;
}
