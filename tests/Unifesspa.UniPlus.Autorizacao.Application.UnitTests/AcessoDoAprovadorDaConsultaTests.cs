namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// A consulta dos pedidos sem decidir, do Encarregado de dados: lê a fila de todos e o pedido alheio, mas não as filas de
/// decisão, e a aprovação e a recusa nem avaliam a consulta, para não registrar uma permissão que não governou. Cecília
/// Moura só consulta os pedidos.
/// </summary>
public sealed class AcessoDoAprovadorDaConsultaTests
{
    private const string Consultar = "autorizacao:pedidos:consultar";

    [Fact(DisplayName = "Quem só consulta os pedidos lista a fila de todos, e a consulta é a decisão que governa")]
    public async Task FilaDeTodos_ComSoAConsulta_Autoriza()
    {
        VerificadorDeTeste verificador = new("cecilia.moura", permitidas: [Consultar]);
        AcessoDoAprovador acesso = (await AcessoDoAprovador.ConsultarAsync(verificador, FinalidadeDoAcesso.Fila, pedidoId: null, default))!;

        DecisaoPermitida? governante = await acesso.AutorizarFilaAsync(verificador, FilaDePedidos.Todos, default);

        governante!.Requisito.Permissao.Should().Be(Consultar);
        verificador.Gravados.Should().ContainSingle().Subject.Should().ContainSingle().Which.Permissao.Should().Be(Consultar);
    }

    [Theory(DisplayName = "Quem só consulta os pedidos não lista as filas de decisão, e a negativa da etapa é a gravada")]
    [InlineData(FilaDePedidos.Gestor, "autorizacao:pedidos:aprovar-funcional")]
    [InlineData(FilaDePedidos.ProtecaoDeDados, "autorizacao:pedidos:aprovar-protecao-de-dados")]
    public async Task FilaDeDecisao_ComSoAConsulta_Recusa(FilaDePedidos fila, string permissaoDaEtapa)
    {
        VerificadorDeTeste verificador = new("cecilia.moura", permitidas: [Consultar]);
        AcessoDoAprovador acesso = (await AcessoDoAprovador.ConsultarAsync(verificador, FinalidadeDoAcesso.Fila, pedidoId: null, default))!;

        DecisaoPermitida? governante = await acesso.AutorizarFilaAsync(verificador, fila, default);

        governante.Should().BeNull();
        RegistroDecisaoAcesso gravado = verificador.Gravados.Should().ContainSingle().Subject.Should().ContainSingle().Subject;
        (gravado.Permissao, gravado.Permitido).Should().Be((permissaoDaEtapa, false));
    }

    [Fact(DisplayName = "Quem só consulta os pedidos vê o pedido alheio, mas não decide etapa nenhuma")]
    public async Task Leitura_ComSoAConsulta_VeSemDecidir()
    {
        VerificadorDeTeste verificador = new("cecilia.moura", permitidas: [Consultar]);

        AcessoDoAprovador acesso = (await AcessoDoAprovador.ConsultarAsync(verificador, FinalidadeDoAcesso.Leitura, Guid.CreateVersion7(), default))!;

        (acesso.VeTodos, acesso.Aprova, acesso.Consulta).Should().Be((true, false, true));
    }

    [Fact(DisplayName = "A decisão de uma etapa não avalia a consulta: nem permite, nem entra no registro")]
    public async Task Decisao_NaoAvaliaAConsulta()
    {
        VerificadorDeTeste verificador = new("cecilia.moura", permitidas: [Consultar]);

        AcessoDoAprovador acesso = (await AcessoDoAprovador.ConsultarAsync(verificador, FinalidadeDoAcesso.Decisao, Guid.CreateVersion7(), default))!;
        await acesso.RegistrarDoDesfechoAsync(verificador, default);

        (acesso.DecisaoConsultar, acesso.Aprova).Should().Be(((DecisaoDeAcesso?)null, false));
        verificador.Gravados.Should().ContainSingle().Subject.Should().HaveCount(2).And.NotContain(r => r.Permissao == Consultar);
    }

    [Fact(DisplayName = "Pedido inexistente pedido por quem consulta os pedidos não grava nada")]
    public async Task Leitura_DoInexistente_ComAConsulta_NaoGrava()
    {
        VerificadorDeTeste verificador = new("cecilia.moura", permitidas: [Consultar]);

        await AcessoDoAprovador.RegistrarNegativasDoInexistenteAsync(verificador, FinalidadeDoAcesso.Leitura, default);

        verificador.Gravados.Should().BeEmpty("quem consulta os pedidos veria o pedido; o inexistente é só um link errado");
    }
}
