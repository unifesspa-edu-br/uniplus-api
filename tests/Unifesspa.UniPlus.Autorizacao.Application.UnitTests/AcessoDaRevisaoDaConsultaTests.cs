namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

/// <summary>
/// A consulta das revisões sem decidir, do Encarregado de dados: lê as revisões, mas não decide item, e a decisão do item
/// recusa quem só consulta antes de travar a revisão, o papel, a pessoa ou a concessão. Cecília Moura só consulta.
/// </summary>
public sealed class AcessoDaRevisaoDaConsultaTests
{
    private const string Consultar = "autorizacao:revisoes:consultar";

    [Fact(DisplayName = "Quem só consulta as revisões lê, mas não decide item de governança nenhuma")]
    public async Task SoConsulta_LeSemDecidir()
    {
        VerificadorDeTeste verificador = new("cecilia.moura", permitidas: [Consultar]);

        AcessoDaRevisao acesso = (await AcessoDaRevisao.ConsultarAsync(verificador, null, CancellationToken.None))!;

        (acesso.Le, acesso.DecideAlgum, acesso.Consulta).Should().Be((true, false, true));
        acesso.Leitura!.Requisito.Permissao.Should().Be(Consultar);
    }

    [Fact(DisplayName = "A leitura de quem só consulta grava a consulta permitida")]
    public async Task SoConsulta_RegistraALeitura()
    {
        VerificadorDeTeste verificador = new("cecilia.moura", permitidas: [Consultar]);
        AcessoDaRevisao acesso = (await AcessoDaRevisao.ConsultarAsync(verificador, Guid.CreateVersion7(), CancellationToken.None))!;

        await acesso.RegistrarLeituraAsync(verificador, CancellationToken.None);

        RegistroDecisaoAcesso gravado = verificador.Gravados.Should().ContainSingle().Subject.Should().ContainSingle().Subject;
        (gravado.Permissao, gravado.Permitido, gravado.ResourceTipo).Should().Be((Consultar, true, "revisao-de-acessos"));
    }

    [Fact(DisplayName = "A decisão do item recusa com 403 quem só consulta, sem tomar trava nenhuma e sem registrar a consulta")]
    public async Task DecisaoDoItem_SoConsulta_RecusaAntesDasTravas()
    {
        VerificadorDeTeste verificador = new("cecilia.moura", permitidas: [Consultar]);
        IRevisaoDeAcessosRepository revisoes = Substitute.For<IRevisaoDeAcessosRepository>();
        IPapelDoRepresentante papel = Substitute.For<IPapelDoRepresentante>();
        IIncompatibilidadeDosPapeis papeis = Substitute.For<IIncompatibilidadeDosPapeis>();
        IConcessaoRepository concessoes = Substitute.For<IConcessaoRepository>();

        Result<DecisaoDoItem> preparada = await new PreparoDaDecisaoDoItem(revisoes, concessoes, papel, papeis, TimeProvider.System)
            .PrepararAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), verificador, new Identidade(Emissor, "cecilia.moura"), CancellationToken.None);

        preparada.Error!.Code.Should().Be(RevisaoErrorCodes.SemPermissao);
        (revisoes.ReceivedCalls().Count(), papel.ReceivedCalls().Count(), papeis.ReceivedCalls().Count(), concessoes.ReceivedCalls().Count()).Should().Be((0, 0, 0, 0));
        verificador.Gravados.Should().ContainSingle().Subject.Should().HaveCount(2).And.OnlyContain(static r => !r.Permitido && r.Permissao != Consultar);
    }
}
