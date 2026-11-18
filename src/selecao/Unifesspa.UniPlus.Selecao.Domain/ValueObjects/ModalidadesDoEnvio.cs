namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>As modalidades apuradas por opção, com as recusas e os fatos registrados no envio.</summary>
public sealed record ModalidadesDoEnvio
{
    private ModalidadesDoEnvio(ModalidadesDeConcorrencia primeira, ModalidadesDeConcorrencia? segunda)
    {
        Primeira = Copiar(primeira);
        Segunda = segunda is null ? null : Copiar(segunda);
    }

    public ModalidadesDeConcorrencia Primeira { get; }
    public ModalidadesDeConcorrencia? Segunda { get; }

    private static ModalidadesDeConcorrencia Copiar(ModalidadesDeConcorrencia modalidades) => modalidades with
    {
        Itens = Array.AsReadOnly(modalidades.Itens.ToArray()),
        Pendencias = Array.AsReadOnly(modalidades.Pendencias.ToArray()),
        Dependencias = new(Array.AsReadOnly(modalidades.Dependencias.Fatos.ToArray()), Array.AsReadOnly(modalidades.Dependencias.Grupos.ToArray())),
    };
    private const string CampoDaSegundaOpcao = "ofertaCursoSegundaOpcaoId";

    public static ModalidadesDoEnvio DaAvaliacao(ResultadoDaAvaliacaoDaInscricao avaliacao, OpcoesDeCursoDaInscricao opcoes)
    {
        ArgumentNullException.ThrowIfNull(avaliacao);
        ArgumentNullException.ThrowIfNull(opcoes);
        return new(avaliacao.Modalidades, opcoes.Segunda is null ? null : avaliacao.ModalidadesSegundaOpcao);
    }

    public IEnumerable<FieldError> Recusas(OpcoesDeCursoDaInscricao opcoes, RespostasAdmitidas admitidas)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(admitidas);
        if (Segunda is not { } segunda || !OpcoesDeCursoDaInscricao.SegundaAdmissivel(opcoes.Primeira, opcoes.Segunda, admitidas))
        {
            return RecusasDaModalidade(Primeira, OpcaoDeCurso.Unica);
        }
        // A mesma pendência nas duas opções é pedida uma vez.
        return RecusasDaModalidade(Primeira, OpcaoDeCurso.Primeira).Concat(RecusasDaModalidade(segunda, OpcaoDeCurso.Segunda)).Distinct();
    }

    public Dictionary<string, JsonElement> ComDerivados(IReadOnlyDictionary<string, JsonElement> derivados)
    {
        Dictionary<string, JsonElement> resultado = PreenchimentoDoCandidato.Mesclar(new Dictionary<string, JsonElement>(StringComparer.Ordinal), derivados);
        if (Primeira.Derivada) resultado[AvaliacaoDaInscricao.FatoDaModalidade] = JsonSerializer.SerializeToElement(Primeira.Itens.Select(static m => m.Codigo));
        if (Segunda is { Derivada: true }) resultado[AvaliacaoDaInscricao.FatoDaModalidadeDaSegundaOpcao] = JsonSerializer.SerializeToElement(Segunda.Itens.Select(static m => m.Codigo));
        return resultado;
    }

    public Dictionary<string, DefinicaoDaResposta> ComDefinicoes(IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes)
    {
        Dictionary<string, DefinicaoDaResposta> resultado = new(definicoes, StringComparer.Ordinal);
        if (Primeira.Derivada) resultado[AvaliacaoDaInscricao.FatoDaModalidade] = AvaliacaoDaInscricao.DefinicaoDaModalidade;
        if (Segunda is { Derivada: true }) resultado[AvaliacaoDaInscricao.FatoDaModalidadeDaSegundaOpcao] = AvaliacaoDaInscricao.DefinicaoDaModalidade;
        return resultado;
    }

    /// <summary>
    /// Com a modalidade derivada, o envio exige que ela esteja decidida e não vazia. A recusa sai em
    /// cada campo de que a modalidade depende, para o candidato conferir as respostas; a modalidade que
    /// falta decidir sai nos campos que faltam responder. Na 2ª opção, quem não concorre a nenhuma
    /// modalidade pode também trocar ou retirar a opção, então a recusa sai nela.
    /// </summary>
    private static IEnumerable<FieldError> RecusasDaModalidade(ModalidadesDeConcorrencia modalidades, OpcaoDeCurso opcao)
    {
        if (!modalidades.Derivada || modalidades.Situacao == SituacaoDasModalidades.Concorre)
        {
            return [];
        }

        if (modalidades.Situacao == SituacaoDasModalidades.NaoConcorre)
        {
            return RecusasSemConcorrencia(modalidades, opcao);
        }
        DomainError naoDecidida = new(
            InscricaoErrorCodes.ModalidadeNaoDecidida,
            "Ainda não é possível saber em quais modalidades você concorre. Responda os campos indicados e envie de novo.");
        return CamposOuAModalidade([.. modalidades.Pendencias.Select(static p => p.FatoCodigo)], opcao == OpcaoDeCurso.Segunda)
            .Select(campo => new FieldError(campo, naoDecidida));
    }

    private static IEnumerable<FieldError> RecusasSemConcorrencia(ModalidadesDeConcorrencia modalidades, OpcaoDeCurso opcao)
    {
        if (opcao == OpcaoDeCurso.Segunda)
        {
            return RecusaDaSegundaOpcao();
        }

        DomainError nenhuma = NenhumaModalidade(opcao);
        return CamposOuAModalidade([.. modalidades.Dependencias.Fatos.Where(static f => !f.Contains('.', StringComparison.Ordinal)), .. modalidades.Dependencias.Grupos], segundaOpcao: false)
            .Select(campo => new FieldError(campo, nenhuma));
    }

    private static IEnumerable<FieldError> RecusaDaSegundaOpcao() =>
        [new FieldError(CampoDaSegundaOpcao, new DomainError(InscricaoErrorCodes.NenhumaModalidade,
            "Com estas respostas, você não concorre a nenhuma das modalidades ofertadas na 2ª opção de curso. "
            + "Escolha outra oferta ou retire a 2ª opção e envie de novo."))];

    private static DomainError NenhumaModalidade(OpcaoDeCurso opcao) => opcao == OpcaoDeCurso.Primeira
                ? new(
                    InscricaoErrorCodes.NenhumaModalidade,
                    "Com estas respostas, você não concorre a nenhuma das modalidades ofertadas na 1ª opção de curso. "
                    + "Confira as respostas indicadas. Se as respostas estão corretas, escolha outra oferta como 1ª opção "
                    + "ou consulte o edital.")
                : new(
                    InscricaoErrorCodes.NenhumaModalidade,
                    "Com estas respostas, você não concorre a nenhuma das modalidades ofertadas neste processo seletivo. "
                    + "Confira as respostas indicadas. Se as respostas estão corretas, você não atende às condições de nenhuma "
                    + "modalidade deste processo; consulte o edital.");

    /// <summary>A opção de curso a que a recusa de modalidade se refere: a única, quando a inscrição não tem 2ª, ou a 1ª ou a 2ª.</summary>
    private enum OpcaoDeCurso
    {
        Unica,
        Primeira,
        Segunda,
    }

    /// <summary>Sem campo a indicar, a recusa sai na modalidade da opção: o fato <c>MODALIDADE</c> é o da 1ª, e a 2ª opção sai no campo dela.</summary>
    private static IReadOnlyList<string> CamposOuAModalidade(IReadOnlyList<string> campos, bool segundaOpcao) =>
        campos.Count > 0 ? campos : [segundaOpcao ? CampoDaSegundaOpcao : AvaliacaoDaInscricao.FatoDaModalidade];
}
