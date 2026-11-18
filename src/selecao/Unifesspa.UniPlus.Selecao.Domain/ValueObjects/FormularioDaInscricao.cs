namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Collections.ObjectModel;
using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;

public sealed class FormularioDaInscricao : IEquatable<FormularioDaInscricao>
{
    private FormularioDaInscricao(Guid versaoFormularioId, PreenchimentoDoCandidato preenchimento,
        IReadOnlyDictionary<string, JsonElement> derivados, IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes)
    {
        VersaoFormularioId = versaoFormularioId;
        Preenchimento = preenchimento;
        Derivados = derivados;
        DefinicoesDasRespostas = definicoes;
    }

    public Guid VersaoFormularioId { get; }
    public PreenchimentoDoCandidato Preenchimento { get; }
    public IReadOnlyDictionary<string, JsonElement> Derivados { get; }
    public IReadOnlyDictionary<string, DefinicaoDaResposta> DefinicoesDasRespostas { get; }

    public bool Equals(FormularioDaInscricao? outro) => outro is not null && VersaoFormularioId == outro.VersaoFormularioId
        && Preenchimento.Equals(outro.Preenchimento) && Derivados.Count == outro.Derivados.Count
        && Derivados.All(d => outro.Derivados.TryGetValue(d.Key, out JsonElement valor) && JsonElement.DeepEquals(d.Value, valor))
        && DefinicoesDasRespostas.Count == outro.DefinicoesDasRespostas.Count
        && DefinicoesDasRespostas.All(d => outro.DefinicoesDasRespostas.TryGetValue(d.Key, out DefinicaoDaResposta? valor) && d.Value == valor);

    public override bool Equals(object? obj) => obj is FormularioDaInscricao outro && Equals(outro);
    public override int GetHashCode() => HashCode.Combine(VersaoFormularioId, Preenchimento, Derivados.Count, DefinicoesDasRespostas.Count);

    /// <summary>
    /// O CPF é número de documento: a resposta dele é sempre gravada cifrada como identificador, qualquer que seja a
    /// classificação congelada no processo — reclassificar o fato não pode deixar o CPF em claro na inscrição nem no comprovante.
    /// </summary>
    private static readonly DefinicaoDaResposta DefinicaoDoCpf =
        DefinicaoDaResposta.De(TipoRenderizacao.Texto, DefinicaoDaResposta.ClassificacaoIdentificador, fatoCodigo: ConjuntoBasicoDaInscricao.Cpf);

    /// <summary>As definições admitidas pela versão, com a do CPF forçada a identificador cifrado.</summary>
    private static Dictionary<string, DefinicaoDaResposta> ComCpfCifrado(IEnumerable<KeyValuePair<string, DefinicaoDaResposta>> definicoes) =>
        new(definicoes, StringComparer.Ordinal) { [ConjuntoBasicoDaInscricao.Cpf] = DefinicaoDoCpf };

    public static FormularioDaInscricao Inicial(Guid versao, Cpf cpf) => new(versao,
        PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null),
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { [ConjuntoBasicoDaInscricao.Cpf] = JsonSerializer.SerializeToElement((cpf ?? throw new ArgumentNullException(nameof(cpf))).Valor) },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), []),
        CopiarDerivados(new Dictionary<string, JsonElement>(StringComparer.Ordinal)), CopiarDefinicoes(ComCpfCifrado([])));

    public static FormularioDaInscricao Reidratar(Guid versao, PreenchimentoDoCandidato preenchimento,
        IReadOnlyDictionary<string, JsonElement> derivados, IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes) =>
        new(versao, preenchimento, CopiarDerivados(derivados), CopiarDefinicoes(definicoes));

    private static ReadOnlyDictionary<string, JsonElement> CopiarDerivados(IReadOnlyDictionary<string, JsonElement> derivados) =>
        new ReadOnlyDictionary<string, JsonElement>(derivados.ToDictionary(static d => d.Key, static d => d.Value.Clone(), StringComparer.Ordinal));

    private static ReadOnlyDictionary<string, DefinicaoDaResposta> CopiarDefinicoes(IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes) =>
        new ReadOnlyDictionary<string, DefinicaoDaResposta>(new Dictionary<string, DefinicaoDaResposta>(definicoes, StringComparer.Ordinal));

    internal FormularioDaInscricao EnviadoComDerivados(IReadOnlyDictionary<string, JsonElement> derivados,
        IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes, MunicipiosConferidos conferidos) =>
        ComDerivados(conferidos.DerivadosComNomes(derivados, definicoes));

    internal FormularioDaInscricao EnviadoComRespostas(IReadOnlyDictionary<string, JsonElement> respostas,
        RespostasAdmitidas admitidas, ResultadoDaAvaliacaoDaInscricao avaliacao, MunicipiosConferidos conferidos)
    {
        PreenchimentoDoCandidato aplicaveis = Preenchimento.CanonizarRespostas(respostas, admitidas.Definicoes).FiltrarRespostas(avaliacao.Formulario.Fatos);
        Dictionary<string, JsonElement> semDerivados = aplicaveis.Respostas.Where(r => !Derivados.ContainsKey(r.Key))
            .ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal);
        return ComPreenchimento(Preenchimento.ComRespostas(conferidos.RespostasComNomes(semDerivados, admitidas.Definicoes)));
    }

    internal FormularioDaInscricao EnviadoComGrupos(IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        RespostasAdmitidas admitidas, ResultadoDaAvaliacaoDaInscricao avaliacao, MunicipiosConferidos conferidos)
    {
        PreenchimentoDoCandidato aplicaveis = Preenchimento.CanonizarGrupos(grupos, admitidas.Definicoes).FiltrarGrupos(avaliacao.Formulario.Grupos);
        return ComPreenchimento(Preenchimento.ComGrupos(aplicaveis.Grupos.ToDictionary(static g => g.Key,
            g => (IReadOnlyList<OcorrenciaDeGrupoInscricao>)[.. g.Value.Select(o => o with
            {
                Respostas = conferidos.SubitensComNomes(o.Respostas, admitidas.Definicoes),
            })], StringComparer.Ordinal)));
    }

    internal FormularioDaInscricao NaVersao(Guid versao) => new(versao, Preenchimento, Derivados, DefinicoesDasRespostas);
    internal FormularioDaInscricao ComPrimeiraAdmitida(RespostasAdmitidas admitidas)
    {
        Guid? primeira = Preenchimento.Opcoes.Primeira is { } escolhida && admitidas.OfertasDeCurso.Contains(escolhida) ? escolhida : null;
        return ComOpcoes(OpcoesDeCursoDaInscricao.Reidratar(primeira, Preenchimento.Opcoes.Segunda));
    }

    internal FormularioDaInscricao ComSegundaAdmitida(RespostasAdmitidas admitidas)
    {
        Guid? primeira = Preenchimento.Opcoes.Primeira;
        Guid? segunda = Preenchimento.Opcoes.Segunda;
        return ComOpcoes(OpcoesDeCursoDaInscricao.Reidratar(primeira,
            OpcoesDeCursoDaInscricao.SegundaAdmissivel(primeira, segunda, admitidas) ? segunda : null));
    }

    internal FormularioDaInscricao ComOpcoes(OpcoesDeCursoDaInscricao opcoes) => ComPreenchimento(Preenchimento.ComOpcoes(opcoes));
    internal FormularioDaInscricao ComRespostasAdmitidas(RespostasAdmitidas admitidas) => ComPreenchimento(Preenchimento.ComRespostasAdmitidas(admitidas));
    internal FormularioDaInscricao ComGruposAdmitidos(RespostasAdmitidas admitidas) => ComPreenchimento(Preenchimento.ComGruposAdmitidos(admitidas));
    internal FormularioDaInscricao ComAceitesAdmitidos(RespostasAdmitidas admitidas) => ComPreenchimento(Preenchimento.ComAceitesAdmitidos(admitidas));
    internal FormularioDaInscricao SalvoComRespostas(IReadOnlyDictionary<string, JsonElement> respostas, RespostasAdmitidas admitidas) =>
        ComPreenchimento(Preenchimento.CanonizarRespostas(respostas, admitidas.Definicoes));
    internal FormularioDaInscricao SalvoComGrupos(IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos, RespostasAdmitidas admitidas) =>
        ComPreenchimento(Preenchimento.CanonizarGrupos(grupos, admitidas.Definicoes));
    internal FormularioDaInscricao ComAceites(IReadOnlyList<AceiteDeTermo> aceites) => ComPreenchimento(Preenchimento.ComAceites(aceites));
    internal FormularioDaInscricao ComPreenchimento(PreenchimentoDoCandidato preenchimento) => new(VersaoFormularioId, preenchimento, Derivados, DefinicoesDasRespostas);
    internal FormularioDaInscricao ComDerivados(IReadOnlyDictionary<string, JsonElement> derivados) => new(VersaoFormularioId, Preenchimento, CopiarDerivados(derivados), DefinicoesDasRespostas);
    internal FormularioDaInscricao ComDefinicoes(IEnumerable<KeyValuePair<string, DefinicaoDaResposta>> definicoes) => new(VersaoFormularioId, Preenchimento, Derivados, CopiarDefinicoes(ComCpfCifrado(definicoes)));

    /// <summary>
    /// O comprovante nunca carrega número de documento (§4.1 do plano da #1855) — o domínio aplica
    /// esta invariante por si, independente do que a Infrastructure faça com o jsonb de respostas.
    /// </summary>
    public Dictionary<string, JsonElement> SemNumerosDeDocumento(IReadOnlyDictionary<string, JsonElement> respostas) =>
        respostas
            .Where(r => DefinicoesDasRespostas.TryGetValue(r.Key, out DefinicaoDaResposta? definicao) && !definicao.Identificador)
            .ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal);

    /// <summary>
    /// As respostas sem as de cifra individual: o comprovante não as guarda em claro junto das demais,
    /// e as leva à parte, para o repositório cifrá-las.
    /// </summary>
    public Dictionary<string, JsonElement> SemCifraIndividual(IReadOnlyDictionary<string, JsonElement> respostas) =>
        respostas
            .Where(r => !TemCifraIndividual(r.Key))
            .ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal);

    public Dictionary<string, JsonElement> ApenasCifraIndividual(IReadOnlyDictionary<string, JsonElement> respostas) =>
        respostas
            .Where(r => TemCifraIndividual(r.Key))
            .ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal);

    private bool TemCifraIndividual(string fatoCodigo) =>
        DefinicoesDasRespostas.TryGetValue(fatoCodigo, out DefinicaoDaResposta? definicao) && definicao.CifraIndividual;

    /// <summary>
    /// A mesma exclusão de <see cref="SemNumerosDeDocumento"/>, aplicada às respostas de cada
    /// ocorrência dentro de cada grupo — um grupo repetível também pode coletar um fato
    /// <c>Identificador</c> por membro (ex.: documento de um dependente).
    /// </summary>
    public Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> SemNumerosDeDocumentoNosGrupos(
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos) =>
        grupos.ToDictionary(
            static g => g.Key,
            g => (IReadOnlyList<OcorrenciaDeGrupoInscricao>)
                [.. g.Value.Select(o => new OcorrenciaDeGrupoInscricao(o.Id, SemCifraIndividual(SemNumerosDeDocumento(o.Respostas))))],
            StringComparer.Ordinal);
}
