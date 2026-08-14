namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// O gate da convenção de contagem (UNI-REQ-0112) nas três transições que geram versão.
/// </summary>
/// <remarks>
/// <para>
/// A exigência é <b>condicional</b>: vale quando alguma contagem do certame distingue dia
/// útil de dia não útil. Como toda regra de recurso declara prazo de interposição, e as duas
/// unidades declaráveis ali correm sobre dia útil, basta existir regra de recurso. Um
/// certame sem recurso nenhum publica sem declarar convenção — e é isso que a contraprova
/// abaixo fixa, para que o gate não vire universal por descuido.
/// </para>
/// <para>
/// O gate precede o de conformidade, então os processos aqui são mínimos de propósito: o que
/// se prova é qual recusa vem primeiro e por quê, não o checklist inteiro.
/// </para>
/// </remarks>
public sealed class GateDoAlgoritmoDeContagemTests
{
    private const string CodigoDaRecusa = "ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado";
    private static readonly string HashFixo = new('a', 64);

    /// <summary>
    /// Processo conforme em tudo o mais — o que se observa aqui é a recusa do gate da
    /// convenção, e ela só é atribuível se nenhuma outra dimensão estiver pendente.
    /// </summary>
    private static ProcessoSeletivo Processo(RegraRecursoFase? regraRecurso) =>
        ProcessoConformeFactory.Criar(fase: ProcessoConformeFactory.FaseConforme(regraRecurso));

    private static ReferenciaRegra Algoritmo() => ReferenciaRegra.Criar(
        AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, "v1", new string('b', 64)).Value!;

    private static RegraRecursoFase RegraDeRecurso(
        UnidadePrazo prazoUnidade = UnidadePrazo.DiasUteis,
        decimal prazoValor = 2m,
        UnidadePrazo? suspensividadeUnidade = null) => RegraRecursoFase.Criar(
            ReferenciaRegra.Criar(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('c', 64)).Value!,
            new ArgsRegraPrazoRecurso(
                PrazoValor: prazoValor,
                PrazoUnidade: prazoUnidade,
                AtoAncoraCodigo: "RESULTADO_FINAL",
                SuspensividadePrimeiraInstanciaValor: suspensividadeUnidade is null ? null : 5m,
                SuspensividadePrimeiraInstanciaUnidade: suspensividadeUnidade,
                SuspensividadeSegundaInstanciaValor: null,
                SuspensividadeSegundaInstanciaUnidade: null)).Value!;

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(
        ProcessoConformeFactory.Dados(),
        configuracaoCongeladaCanonica: "{}"u8.ToArray(),
        schemaVersion: "1.1",
        algoritmoHash: "canonical-json/sha256@v1",
        hashDocumento: HashFixo,
        atorUsuarioSub: "teste",
        TimeProvider.System);

    /// <summary>
    /// Leva o processo ao estado publicado sem passar pelo gate — a publicação é assunto de
    /// outro teste, e aqui o que interessa é o comportamento das transições posteriores.
    /// </summary>
    private static VersaoConfiguracao PublicarComAlgoritmo(ProcessoSeletivo processo)
    {
        processo.DefinirAlgoritmoContagemPrazo(Algoritmo(), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> publicada = Publicar(processo);
        publicada.IsSuccess.Should().BeTrue(publicada.Error?.Message);
        return publicada.Value!;
    }

    private static void RetirarAlgoritmo(ProcessoSeletivo processo) =>
        typeof(ProcessoSeletivo)
            .GetProperty(nameof(ProcessoSeletivo.AlgoritmoContagemPrazo))!
            .SetValue(processo, null);

    [Fact(DisplayName = "Publicação inicial: prazo em dias úteis sem convenção declarada é recusado com erro nomeado")]
    public void PublicacaoInicial_SemAlgoritmo_Recusa()
    {
        ProcessoSeletivo processo = Processo(RegraDeRecurso());

        Result<VersaoConfiguracao> publicar = Publicar(processo);

        publicar.IsFailure.Should().BeTrue();
        publicar.Error!.Code.Should().Be(CodigoDaRecusa);
    }

    [Fact(DisplayName = "Retificação em ato único: a mesma recusa, pela mesma razão — congela versão vinculante igual")]
    public void RetificacaoEmAtoUnico_SemAlgoritmo_Recusa()
    {
        ProcessoSeletivo processo = Processo(RegraDeRecurso());
        VersaoConfiguracao versaoAtual = PublicarComAlgoritmo(processo);

        // O estado não é alcançável pela API hoje; o gate é invariante da raiz e tem de
        // pegá-lo independentemente de como surgiu — correção de dados no banco inclusive.
        RetirarAlgoritmo(processo);

        Result<VersaoConfiguracao> retificar = processo.Retificar(
            ProcessoConformeFactory.Dados(),
            versaoAtual,
            configuracaoCongeladaCanonica: "{}"u8.ToArray(),
            schemaVersion: "1.1",
            algoritmoHash: "canonical-json/sha256@v1",
            hashDocumento: HashFixo,
            atorUsuarioSub: "teste",
            motivo: "Correção do prazo",
            TimeProvider.System);

        retificar.IsFailure.Should().BeTrue();
        retificar.Error!.Code.Should().Be(CodigoDaRecusa);
    }

    [Fact(DisplayName = "Fechamento de sessão editorial: a terceira transição recusa com o mesmo erro nomeado")]
    public void FechamentoDeSessao_SemAlgoritmo_Recusa()
    {
        ProcessoSeletivo processo = Processo(RegraDeRecurso());
        VersaoConfiguracao versaoAtual = PublicarComAlgoritmo(processo);

        processo.AbrirRetificacao("Ajustar o prazo", versaoAtual, "teste", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();

        RetirarAlgoritmo(processo);

        Result<VersaoConfiguracao> fechar = processo.FecharRetificacao(
            ProcessoConformeFactory.Dados(),
            versaoAtual,
            configuracaoCongeladaCanonica: "{}"u8.ToArray(),
            schemaVersion: "1.1",
            algoritmoHash: "canonical-json/sha256@v1",
            hashDocumento: HashFixo,
            atorUsuarioSub: "teste",
            PrecondicaoIfMatch.Curinga,
            TimeProvider.System);

        fechar.IsFailure.Should().BeTrue();
        fechar.Error!.Code.Should().Be(CodigoDaRecusa);
    }

    [Fact(DisplayName = "Prazo de interposição em HORAS também exige a convenção — só as horas em dia útil avançam o relógio")]
    public void InterposicaoEmHoras_SemAlgoritmo_Recusa()
    {
        ProcessoSeletivo processo = Processo(RegraDeRecurso(prazoUnidade: UnidadePrazo.Horas, prazoValor: 48m));

        Result<VersaoConfiguracao> publicar = Publicar(processo);

        publicar.IsFailure.Should().BeTrue();
        publicar.Error!.Code.Should().Be(CodigoDaRecusa);
    }

    [Fact(DisplayName = "Contraprova: certame SEM regra de recurso publica sem declarar convenção — não há o que contar")]
    public void SemRegraDeRecurso_NaoExigeAlgoritmo()
    {
        ProcessoSeletivo processo = Processo(regraRecurso: null);

        Result<VersaoConfiguracao> publicar = Publicar(processo);

        publicar.Error?.Code.Should().NotBe(CodigoDaRecusa,
            "sem prazo de recurso não há contagem que distinga dia útil, e exigir a convenção seria pedir "
                + "uma declaração que não governa nada");
    }

    [Fact(DisplayName = "Contraprova: suspensividade em dias corridos, com interposição em horas, não muda a exigência")]
    public void SuspensividadeCorrida_NaoAlteraAExigencia()
    {
        ProcessoSeletivo comAlgoritmo = Processo(
            RegraDeRecurso(prazoUnidade: UnidadePrazo.Horas, prazoValor: 48m, suspensividadeUnidade: UnidadePrazo.Dias));
        comAlgoritmo.DefinirAlgoritmoContagemPrazo(Algoritmo(), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Publicar(comAlgoritmo).Error?.Code.Should().NotBe(CodigoDaRecusa,
            "declarada a convenção, o gate não tem mais o que recusar — a suspensividade corrida nunca foi o "
                + "que o exigia, e a interposição em horas já era coberta pela declaração");
    }

    [Fact(DisplayName = "Declarada a convenção, o gate deixa passar — a recusa que reste é de outra dimensão")]
    public void ComAlgoritmo_GateNaoRecusa()
    {
        ProcessoSeletivo processo = Processo(RegraDeRecurso());
        processo.DefinirAlgoritmoContagemPrazo(Algoritmo(), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Publicar(processo).Error?.Code.Should().NotBe(CodigoDaRecusa);
    }

    [Fact(DisplayName = "Declarar a convenção move o ETag da sessão — sem isso, escrita concorrente com revisão velha passaria")]
    public void DeclararAlgoritmo_MoveOETagDaSessao()
    {
        ProcessoSeletivo processo = Processo(RegraDeRecurso());
        VersaoConfiguracao versaoAtual = PublicarComAlgoritmo(processo);

        processo.AbrirRetificacao("Trocar a convenção", versaoAtual, "teste", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();
        string? etagAntes = processo.ETagDaSessaoEditorial;
        etagAntes.Should().NotBeNull("pré-condição: a sessão está aberta e tem ETag");

        processo.DefinirAlgoritmoContagemPrazo(
            ReferenciaRegra.Criar(AlgoritmoContagemPrazoCodigo.AvancaDataUtil, "v1", new string('d', 64)).Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        processo.ETagDaSessaoEditorial.Should().NotBe(etagAntes);
    }
}
