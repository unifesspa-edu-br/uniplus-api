namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Invariantes do vínculo genérico ato ↔ entidade (ADR-0105): o rótulo do tipo é
/// opaco mas tem forma canônica, o identificador da entidade é obrigatório, o
/// vínculo nasce do próprio ato, e um ato sem vínculo é válido.
/// </summary>
public sealed class VinculoAtoEntidadeTests
{
    private static readonly AtoNormativoDeTeste Base = new() { CongelaConfiguracao = true };
    private static readonly Guid Processo = Guid.CreateVersion7();
    private static readonly Guid Chamada = Guid.CreateVersion7();

    [Fact(DisplayName = "Ato registrado sem vínculo é válido — há atos que não tratam de objeto algum")]
    public void Registrar_SemVinculos_AtoValido()
    {
        AtoNormativo ato = Base.Registrar();

        ato.Vinculos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Um mesmo ato vincula-se a mais de uma entidade")]
    public void Registrar_ComDuasEntidades_VinculaAsDuas()
    {
        AtoNormativo ato = (Base with { Vinculos = [new("PROCESSO_SELETIVO", Processo), new("CHAMADA", Chamada)] }).Registrar();

        ato.Vinculos.Should().HaveCount(2);
        ato.Vinculos.Should().OnlyContain(v => v.AtoId == ato.Id);
        ato.Vinculos.Select(v => v.EntidadeId).Should().BeEquivalentTo([Processo, Chamada]);
    }

    public static TheoryData<string, string> TiposAceitosNoRegistro { get; } = new()
    {
        { "PROCESSO_SELETIVO", "PROCESSO_SELETIVO" },
        { "APLICACAO_PROVA_2", "APLICACAO_PROVA_2" },
        { new string('A', 60), new string('A', 60) },
    };

    [Theory(DisplayName = "O registro com um vínculo guarda exatamente esse vínculo, preservado e atrelado ao ato")]
    [MemberData(nameof(TiposAceitosNoRegistro))]
    public void Registrar_ComUmVinculo_GuardaSoEle(string tipoInformado, string tipoGuardado)
    {
        AtoNormativo ato = (Base with { Vinculos = [new(tipoInformado, Processo)] }).Registrar();

        VinculoAtoEntidade vinculo = ato.Vinculos.Single();
        vinculo.AtoId.Should().Be(ato.Id);
        vinculo.EntidadeTipo.Should().Be(tipoGuardado);
        vinculo.EntidadeId.Should().Be(Processo);
    }

    [Fact(DisplayName = "O vínculo aponta para o ato que o criou, e o tipo é preservado")]
    public void Criar_PreservaTipoEAtrelaAoAto()
    {
        AtoNormativo ato = Base.Registrar();

        VinculoAtoEntidade vinculo = VinculoAtoEntidade.Criar(ato, new EntidadeVinculada("PROCESSO_SELETIVO", Processo));

        vinculo.AtoId.Should().Be(ato.Id);
        vinculo.EntidadeTipo.Should().Be("PROCESSO_SELETIVO");
        vinculo.EntidadeId.Should().Be(Processo);
        vinculo.Id.Should().NotBe(Guid.Empty);
    }

    /// <remarks>
    /// O rótulo é opaco — o módulo não sabe o que significa —, mas a grafia é única: sem isso
    /// a mesma entidade se partiria em duas na consulta.
    /// </remarks>
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
        AtoNormativo ato = Base.Registrar();

        Action acao = () => VinculoAtoEntidade.Criar(ato, new EntidadeVinculada(tipo, Processo));

        acao.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Tipo de entidade com dígitos é aceito — o rótulo é opaco")]
    public void Criar_TipoComDigitos_Aceita()
    {
        AtoNormativo ato = Base.Registrar();

        VinculoAtoEntidade.Criar(ato, new EntidadeVinculada("APLICACAO_PROVA_2", Processo)).EntidadeTipo.Should().Be("APLICACAO_PROVA_2");
    }

    [Fact(DisplayName = "Tipo de entidade com mais de 60 caracteres como recebido é recusado")]
    public void Criar_TipoAcimaDoLimiteComoRecebido_Recusa()
    {
        AtoNormativo ato = Base.Registrar();

        Action acao = () => VinculoAtoEntidade.Criar(ato, new EntidadeVinculada(new string('A', 61), Processo));

        acao.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Tipo de entidade com exatamente 60 caracteres como recebido é aceito")]
    public void Criar_TipoNoLimiteComoRecebido_Aceita()
    {
        AtoNormativo ato = Base.Registrar();

        VinculoAtoEntidade.Criar(ato, new EntidadeVinculada(new string('A', 60), Processo)).EntidadeTipo.Should().HaveLength(60);
    }

    [Fact(DisplayName = "Identificador vazio de entidade é recusado")]
    public void Criar_EntidadeIdVazio_Recusa()
    {
        AtoNormativo ato = Base.Registrar();

        Action acao = () => VinculoAtoEntidade.Criar(ato, new EntidadeVinculada("PROCESSO_SELETIVO", Guid.Empty));

        acao.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "Tipo de entidade nulo ou em branco é recusado pelo parâmetro do tipo")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Criar_TipoNuloOuEmBranco_RecusaPeloTipo(string? tipo)
    {
        AtoNormativo ato = Base.Registrar();

        Action acao = () => VinculoAtoEntidade.Criar(ato, new EntidadeVinculada(tipo!, Processo));

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("entidadeTipo");
    }

    [Fact(DisplayName = "Com o rótulo e o identificador inválidos, vence o rótulo")]
    public void Criar_TipoEIdInvalidos_RecusaPrimeiroOTipo()
    {
        AtoNormativo ato = Base.Registrar();

        Action acao = () => VinculoAtoEntidade.Criar(ato, new EntidadeVinculada("x", Guid.Empty));

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("entidadeTipo");
    }

    [Fact(DisplayName = "Só o identificador vazio é recusado pelo parâmetro do identificador")]
    public void Criar_SoIdVazio_RecusaPeloId()
    {
        AtoNormativo ato = Base.Registrar();

        Action acao = () => VinculoAtoEntidade.Criar(ato, new EntidadeVinculada("PROCESSO_SELETIVO", Guid.Empty));

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("entidadeId");
    }

    [Fact(DisplayName = "A entidade do vínculo devolve o rótulo guardado e o identificador")]
    public void Entidade_DevolveOParGuardado()
    {
        VinculoAtoEntidade vinculo = VinculoAtoEntidade.Criar(Base.Registrar(), new EntidadeVinculada("CHAMADA", Chamada));

        vinculo.Entidade.Should().Be(new EntidadeVinculada("CHAMADA", Chamada));
    }

    [Fact(DisplayName = "A mesma entidade vinculada duas vezes ao mesmo ato é recusada")]
    public void Registrar_MesmaEntidadeDuasVezes_Recusa()
    {
        Action acao = () => (Base with { Vinculos = [new("PROCESSO_SELETIVO", Processo), new("PROCESSO_SELETIVO", Processo)] }).Registrar();

        acao.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "A mesma entidade repetida é recusada mesmo com outro vínculo entre as duas ocorrências")]
    public void Registrar_MesmaEntidadeEmOutraOrdem_Recusa()
    {
        Action acao = () => (Base with
        {
            Vinculos = [new("PROCESSO_SELETIVO", Processo), new("CHAMADA", Chamada), new("PROCESSO_SELETIVO", Processo)],
        }).Registrar();

        acao.Should().Throw<ArgumentException>();
    }

    /// <remarks>
    /// O par (tipo, id) é a chave: o mesmo Guid sob rótulos diferentes designa objetos diferentes —
    /// o módulo não interpreta nenhum dos dois.
    /// </remarks>
    [Fact(DisplayName = "A mesma entidade em tipos distintos não é duplicata")]
    public void Registrar_MesmoIdEmTiposDistintos_Aceita()
    {
        AtoNormativo ato = (Base with { Vinculos = [new("PROCESSO_SELETIVO", Processo), new("CHAMADA", Processo)] }).Registrar();

        ato.Vinculos.Should().HaveCount(2);
    }

    [Fact(DisplayName = "A vaga do objeto é reservada em nome da linhagem, não do ato")]
    public void VagaDeLinhagem_CriaComRaizDaLinhagem()
    {
        AtoNormativo raiz = (Base with { UnicoPorObjeto = true, Vinculos = [new("PROCESSO_SELETIVO", Processo)] }).Registrar();

        VagaDeLinhagem vaga = VagaDeLinhagem.Criar(raiz, raiz.Vinculos.Single(), raizId: raiz.Id);

        vaga.EntidadeTipo.Should().Be("PROCESSO_SELETIVO");
        vaga.EntidadeId.Should().Be(Processo);
        vaga.TipoCodigo.Should().Be(raiz.TipoCodigo);
        vaga.RaizId.Should().Be(raiz.Id);
        vaga.AtoId.Should().Be(raiz.Id);
    }

    [Fact(DisplayName = "Um ato de tipo não único por objeto não reserva vaga alguma")]
    public void VagaDeLinhagem_AtoNaoUnico_Recusa()
    {
        AtoNormativo ato = (Base with { Vinculos = [new("PROCESSO_SELETIVO", Processo)] }).Registrar();

        Action acao = () => VagaDeLinhagem.Criar(ato, ato.Vinculos.Single(), raizId: ato.Id);

        acao.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "A raiz da linhagem vazia é recusada")]
    public void VagaDeLinhagem_RaizVazia_Recusa()
    {
        AtoNormativo ato = (Base with { UnicoPorObjeto = true, Vinculos = [new("PROCESSO_SELETIVO", Processo)] }).Registrar();

        Action acao = () => VagaDeLinhagem.Criar(ato, ato.Vinculos.Single(), raizId: Guid.Empty);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("raizId");
    }

    [Fact(DisplayName = "Raiz vazia e ato não único: a raiz vazia é recusada primeiro")]
    public void VagaDeLinhagem_RaizVaziaEAtoNaoUnico_RecusaPelaRaiz()
    {
        AtoNormativo ato = (Base with { Vinculos = [new("PROCESSO_SELETIVO", Processo)] }).Registrar();

        Action acao = () => VagaDeLinhagem.Criar(ato, ato.Vinculos.Single(), raizId: Guid.Empty);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("raizId");
    }
}
