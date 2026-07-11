namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

/// <summary>
/// Invariantes do vínculo genérico ato ↔ entidade (ADR-0105): o rótulo do tipo é
/// opaco mas tem forma canônica, o identificador da entidade é obrigatório, o
/// vínculo nasce do próprio ato, e um ato sem vínculo é válido.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class VinculoAtoEntidadeTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateOnly Publicacao = new(2026, 3, 13);
    private static readonly DateTimeOffset Registro = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);
    private static readonly Guid Processo = Guid.CreateVersion7();
    private static readonly Guid Chamada = Guid.CreateVersion7();

    [Fact(DisplayName = "Ato registrado sem vínculo é válido — há atos que não tratam de objeto algum")]
    public void Registrar_SemVinculos_AtoValido()
    {
        AtoNormativo ato = Novo();

        ato.Vinculos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Um mesmo ato vincula-se a mais de uma entidade")]
    public void Registrar_ComDuasEntidades_VinculaAsDuas()
    {
        AtoNormativo ato = Novo(vinculos:
        [
            ("PROCESSO_SELETIVO", Processo),
            ("CHAMADA", Chamada),
        ]);

        ato.Vinculos.Should().HaveCount(2);
        ato.Vinculos.Should().OnlyContain(v => v.AtoId == ato.Id);
        ato.Vinculos.Select(v => v.EntidadeId).Should().BeEquivalentTo([Processo, Chamada]);
    }

    [Fact(DisplayName = "O vínculo aponta para o ato que o criou, e o tipo é normalizado")]
    public void Criar_NormalizaTipoEAtrelaAoAto()
    {
        AtoNormativo ato = Novo(vinculos: [("  PROCESSO_SELETIVO  ", Processo)]);

        VinculoAtoEntidade vinculo = ato.Vinculos.Single();
        vinculo.AtoId.Should().Be(ato.Id);
        vinculo.EntidadeTipo.Should().Be("PROCESSO_SELETIVO");
        vinculo.EntidadeId.Should().Be(Processo);
        vinculo.Id.Should().NotBe(Guid.Empty);
    }

    [Theory(DisplayName = "Tipo de entidade fora da forma canônica é recusado")]
    [InlineData("processo_seletivo")]
    [InlineData("Processo_Seletivo")]
    [InlineData("PROCESSO-SELETIVO")]
    [InlineData("PROCESSO SELETIVO")]
    [InlineData("_PROCESSO")]
    [InlineData("PROCESSO_")]
    [InlineData("PROCESSO__SELETIVO")]
    public void Criar_TipoForaDaFormaCanonica_Recusa(string tipo)
    {
        // O rótulo é opaco — o módulo não sabe o que significa —, mas a grafia é
        // única: sem isso a mesma entidade se partiria em duas na consulta.
        Action acao = () => Novo(vinculos: [(tipo, Processo)]);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Tipo de entidade com dígitos é aceito — o rótulo é opaco")]
    public void Criar_TipoComDigitos_Aceita()
    {
        AtoNormativo ato = Novo(vinculos: [("APLICACAO_PROVA_2", Processo)]);

        ato.Vinculos.Single().EntidadeTipo.Should().Be("APLICACAO_PROVA_2");
    }

    [Fact(DisplayName = "Identificador vazio de entidade é recusado")]
    public void Criar_EntidadeIdVazio_Recusa()
    {
        Action acao = () => Novo(vinculos: [("PROCESSO_SELETIVO", Guid.Empty)]);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "A mesma entidade vinculada duas vezes ao mesmo ato é recusada")]
    public void Registrar_MesmaEntidadeDuasVezes_Recusa()
    {
        Action acao = () => Novo(vinculos:
        [
            ("PROCESSO_SELETIVO", Processo),
            ("PROCESSO_SELETIVO", Processo),
        ]);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "A mesma entidade em tipos distintos não é duplicata")]
    public void Registrar_MesmoIdEmTiposDistintos_Aceita()
    {
        // O par (tipo, id) é a chave: o mesmo Guid sob rótulos diferentes designa
        // objetos diferentes — o módulo não interpreta nenhum dos dois.
        AtoNormativo ato = Novo(vinculos:
        [
            ("PROCESSO_SELETIVO", Processo),
            ("CHAMADA", Processo),
        ]);

        ato.Vinculos.Should().HaveCount(2);
    }

    [Fact(DisplayName = "A vaga do objeto é reservada em nome da linhagem, não do ato")]
    public void LinhagemUnicaPorObjeto_CriaComRaizDaLinhagem()
    {
        AtoNormativo raiz = Novo(unicoPorObjeto: true, vinculos: [("PROCESSO_SELETIVO", Processo)]);

        LinhagemUnicaPorObjeto vaga = LinhagemUnicaPorObjeto.Criar(
            raiz, raiz.Vinculos.Single(), raizId: raiz.Id);

        vaga.EntidadeTipo.Should().Be("PROCESSO_SELETIVO");
        vaga.EntidadeId.Should().Be(Processo);
        vaga.TipoCodigo.Should().Be(raiz.TipoCodigo);
        vaga.RaizId.Should().Be(raiz.Id);
        vaga.AtoId.Should().Be(raiz.Id);
    }

    [Fact(DisplayName = "Um ato de tipo não único por objeto não reserva vaga alguma")]
    public void LinhagemUnicaPorObjeto_AtoNaoUnico_Recusa()
    {
        AtoNormativo ato = Novo(unicoPorObjeto: false, vinculos: [("PROCESSO_SELETIVO", Processo)]);

        Action acao = () => LinhagemUnicaPorObjeto.Criar(ato, ato.Vinculos.Single(), raizId: ato.Id);

        acao.Should().Throw<InvalidOperationException>();
    }

    private static AtoNormativo Novo(
        bool unicoPorObjeto = false,
        IEnumerable<(string EntidadeTipo, Guid EntidadeId)>? vinculos = null) =>
        AtoNormativo.Registrar(
            orgao: "CEPS",
            serie: "EDITAL",
            ano: 2026,
            numero: "13",
            tipoCodigo: "EDITAL_ABERTURA",
            congelaConfiguracao: true,
            efeitoIrreversivel: false,
            unicoPorObjeto: unicoPorObjeto,
            dataPublicacao: Publicacao,
            documentoHash: HashValido,
            assinante: "Jairo Belchior",
            registradoEm: Registro,
            versaoInvocada: null,
            vinculos: vinculos);
}
