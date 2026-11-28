namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>A forma do pedido de publicação de ato, conferida sem ler nada.</summary>
public sealed class PedidoDoAtoDoProcessoTests
{
    private static readonly DateOnly DataDoAto = new(2026, 12, 10);

    private static PublicarAtoAvulsoDoProcessoCommand Comando(
        IReadOnlyList<AnexoSolicitadoDoAto>? anexos = null, Guid? atoCorrigidoId = null, string? motivo = null) =>
        new(Guid.CreateVersion7(), "AVISO", "5/2026", DataDoAto, "Diretor do CEPS", Guid.CreateVersion7(), anexos, atoCorrigidoId, motivo);

    [Fact(DisplayName = "O pedido completo não tem recusa de forma e manda ler todos os documentos")]
    public void PedidoCompleto_SemRecusaDeForma()
    {
        AnexoSolicitadoDoAto[] anexos = [new(Guid.CreateVersion7(), "Quadro"), new(Guid.CreateVersion7(), "Tabela")];

        PedidoDoAtoDoProcesso pedido = PedidoDoAtoDoProcesso.De(Comando(anexos), []);

        pedido.Recusas.Should().BeEmpty();
        pedido.Documentos.ALer.Select(d => d.Campo).Should().Equal("documentoPrincipalId", "anexos[0].documentoId", "anexos[1].documentoId");
        pedido.Correcao.Situacao.Should().Be(SituacaoDaCorrecaoPedida.SemCorrecao);
    }

    [Fact(DisplayName = "As recusas de forma saem na ordem do pedido: o que é próprio do tipo de pedido, o assinante, os documentos e a correção")]
    public void Recusas_SaemNaOrdemDoPedido()
    {
        Guid repetido = Guid.CreateVersion7();
        FieldError propria = new("tipoAtoCodigo", new DomainError("uniplus.validacao.obrigatorio", "Informe o tipo do ato."));
        PublicarAtoAvulsoDoProcessoCommand comando = Comando([new(repetido, "A"), new(repetido, "B")], motivo: "Erro") with { Assinante = " " };

        PedidoDoAtoDoProcesso pedido = PedidoDoAtoDoProcesso.De(comando, [propria]);

        pedido.Recusas.Select(r => r.Field).Should().Equal("tipoAtoCodigo", "assinante", "anexos[1].documentoId", "atoCorrigidoId");
    }

    [Fact(DisplayName = "Todo campo ausente é recusado como obrigatório, na ordem do pedido: tipo, data, assinante, documento principal e anexo")]
    public void CamposAusentes_SaemComoObrigatoriosNaOrdemDoPedido()
    {
        FieldError propria = AusenciasNosDadosDoAto.Tipo();
        PublicarAtoAvulsoDoProcessoCommand comando = Comando([new(null, "Quadro")]) with
        {
            DataDoAto = null,
            Assinante = null,
            DocumentoPrincipalId = null,
        };

        PedidoDoAtoDoProcesso pedido = PedidoDoAtoDoProcesso.De(comando, [propria]);

        pedido.Recusas.Select(r => r.Field).Should().Equal(
            "tipoAtoCodigo", "dataDoAto", "assinante", "documentoPrincipalId", "anexos[0].documentoId");
        pedido.Recusas.Should().OnlyContain(static r => r.Error.Code == CodigosDeValidacao.Obrigatorio);
        pedido.Documentos.PodeLer.Should().BeFalse("não há documento citado para ler");
    }

    [Fact(DisplayName = "O documento ausente não é lido, e o que foi citado segue para a leitura")]
    public void DocumentoAusente_NaoELidoEOsCitadosSim()
    {
        Guid citado = Guid.CreateVersion7();

        PedidoDosDocumentosDoAto documentos = PedidoDosDocumentosDoAto.De(null, [new(citado, "A"), new(null, "B")]);

        documentos.Recusas.Select(r => r.Field).Should().Equal("documentoPrincipalId", "anexos[1].documentoId");
        documentos.ALer.Select(d => d.Campo).Should().Equal("anexos[0].documentoId");
    }

    [Fact(DisplayName = "A correção recusada vem depois dos documentos e acumula com a quantidade de anexos acima do limite")]
    public void CorrecaoRecusada_VemDepoisDosDocumentosEAcumulaComAQuantidade()
    {
        AnexoSolicitadoDoAto[] anexos = [.. Enumerable.Range(0, 21).Select(static _ => new AnexoSolicitadoDoAto(Guid.CreateVersion7(), "Quadro"))];

        PedidoDoAtoDoProcesso pedido = PedidoDoAtoDoProcesso.De(Comando(anexos, atoCorrigidoId: Guid.CreateVersion7()), []);

        pedido.Recusas.Select(r => r.Field).Should().Equal("anexos", "motivoDaCorrecao");
        pedido.Recusas[0].Error.Code.Should().Be(AtoDoProcessoErrorCodes.QuantidadeDeAnexosExcedida);
    }

    [Fact(DisplayName = "O motivo sem ato corrigido acumula com a data e o documento principal ausentes, a correção por último")]
    public void MotivoSemAto_AcumulaComDataEDocumentoAusentes()
    {
        PublicarAtoAvulsoDoProcessoCommand comando = Comando(motivo: "Data errada") with { DataDoAto = null, DocumentoPrincipalId = null };

        PedidoDoAtoDoProcesso pedido = PedidoDoAtoDoProcesso.De(comando, []);

        pedido.Recusas.Select(r => r.Field).Should().Equal("dataDoAto", "documentoPrincipalId", "atoCorrigidoId");
    }

    [Fact(DisplayName = "Mais de 20 anexos é recusado no campo dos anexos e nenhum documento é lido")]
    public void AnexosAlemDoLimite_NaoLeDocumento()
    {
        AnexoSolicitadoDoAto[] anexos = [.. Enumerable.Range(0, 21).Select(i => new AnexoSolicitadoDoAto(Guid.CreateVersion7(), $"anexo {i}"))];

        PedidoDosDocumentosDoAto documentos = PedidoDosDocumentosDoAto.De(Guid.CreateVersion7(), anexos);

        FieldError recusa = documentos.Recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("anexos");
        recusa.Error.Code.Should().Be(AtoDoProcessoErrorCodes.QuantidadeDeAnexosExcedida);
        documentos.PodeLer.Should().BeFalse();
    }

    [Fact(DisplayName = "Vinte anexos é o limite aceito")]
    public void VinteAnexos_SaoAceitos()
    {
        AnexoSolicitadoDoAto[] anexos = [.. Enumerable.Range(0, 20).Select(i => new AnexoSolicitadoDoAto(Guid.CreateVersion7(), $"anexo {i}"))];

        PedidoDosDocumentosDoAto documentos = PedidoDosDocumentosDoAto.De(Guid.CreateVersion7(), anexos);

        documentos.Recusas.Should().BeEmpty();
        documentos.ALer.Should().HaveCount(21);
    }

    [Fact(DisplayName = "O documento repetido tem uma causa por ocorrência repetida, no campo dela, e só a primeira ocorrência é lida")]
    public void DocumentoRepetido_UmaCausaPorOcorrenciaRepetida()
    {
        Guid principal = Guid.CreateVersion7();
        AnexoSolicitadoDoAto[] anexos = [new(principal, "A"), new(Guid.CreateVersion7(), "B"), new(principal, "C")];

        PedidoDosDocumentosDoAto documentos = PedidoDosDocumentosDoAto.De(principal, anexos);

        documentos.Recusas.Select(r => r.Field).Should().Equal("anexos[0].documentoId", "anexos[2].documentoId");
        documentos.ALer.Select(d => d.Campo).Should().Equal("documentoPrincipalId", "anexos[1].documentoId");
    }

    [Fact(DisplayName = "A correção diz sem correção, corrige um ato ou recusada na forma")]
    public void Correcao_TemTresSituacoes()
    {
        Guid ato = Guid.CreateVersion7();

        PedidoDoAtoDoProcesso sem = PedidoDoAtoDoProcesso.De(Comando(), []);
        PedidoDoAtoDoProcesso corrige = PedidoDoAtoDoProcesso.De(Comando(atoCorrigidoId: ato, motivo: "Erro"), []);
        PedidoDoAtoDoProcesso semMotivo = PedidoDoAtoDoProcesso.De(Comando(atoCorrigidoId: ato), []);
        PedidoDoAtoDoProcesso motivoSemAto = PedidoDoAtoDoProcesso.De(Comando(motivo: "Erro"), []);
        PedidoDoAtoDoProcesso invalido = PedidoDoAtoDoProcesso.De(Comando(atoCorrigidoId: Guid.Empty, motivo: "Erro"), []);

        sem.Correcao.Situacao.Should().Be(SituacaoDaCorrecaoPedida.SemCorrecao);
        corrige.Correcao.Should().Be(new CorrecaoPedida(SituacaoDaCorrecaoPedida.CorrigeUmAto, ato));
        semMotivo.Correcao.Should().Be(new CorrecaoPedida(SituacaoDaCorrecaoPedida.CorrigeUmAto, ato), "só o motivo falta: o ato corrigido pode ser consultado");
        motivoSemAto.Correcao.Situacao.Should().Be(SituacaoDaCorrecaoPedida.RecusadaNaForma);
        invalido.Correcao.Situacao.Should().Be(SituacaoDaCorrecaoPedida.RecusadaNaForma);
    }
}
