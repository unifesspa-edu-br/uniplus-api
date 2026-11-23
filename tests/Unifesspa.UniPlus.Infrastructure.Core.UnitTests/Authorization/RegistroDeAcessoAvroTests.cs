namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using Avro.IO;
using Avro.Specific;

using AwesomeAssertions;

using unifesspa.uniplus.autorizacao.registrodeacesso;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization.Avro;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization.RegistroDeAcessoDeTeste;

/// <summary>
/// O valor Avro do registro de acesso: o lote serializado é lido de volta pelo esquema embutido e as propriedades
/// conferem com o registro de origem; campo novo no registro sem campo no esquema, ou o contrário, quebra o teste.
/// </summary>
public sealed class RegistroDeAcessoAvroTests
{
    [Fact(DisplayName = "O lote com permitido, negado sem id de resource e impedido volta igual depois de serializado e lido pelo esquema")]
    public void LoteSerializado_VoltaIgual()
    {
        Guid inscricao = Guid.CreateVersion7();
        RegistroDecisaoAcesso[] origem = [Registro(clienteOidc: "selecao-web", resourceId: inscricao), Registro(resultado: ResultadoDoAcesso.Negado), Registro(resultado: ResultadoDoAcesso.Impedido)];

        LoteDoRegistroDeAcesso lido = IdaEVolta(MapeadorDoRegistroDeAcesso.ParaLote(origem));

        lido.Registros.Should().HaveCount(3);
        for (int i = 0; i < origem.Length; i++)
        {
            ConferirRegistro(lido.Registros[i], origem[i]);
        }
    }

    [Fact(DisplayName = "O registro da leitura por titularidade mantém nulos os campos da concessão")]
    public void Titularidade_MantemNulosOsCamposDaConcessao()
    {
        RegistroDecisaoAcesso origem = PorTitularidade(Guid.CreateVersion7());

        RegistroDeAcesso lido = IdaEVolta(MapeadorDoRegistroDeAcesso.ParaLote([origem])).Registros[0].Registro;

        lido.FonteGrant.Should().Be("titularidade");
        lido.GrantId.Should().BeNull();
        lido.PerfilId.Should().BeNull();
        lido.VersaoId.Should().BeNull();
        lido.EscopoUnidadeId.Should().BeNull();
        lido.MotivoNegativa.Should().BeNull();
    }

    [Fact(DisplayName = "O instante é gravado em microssegundos e volta sem perda")]
    public void Instante_VoltaSemPerdaEmMicrossegundos()
    {
        RegistroDecisaoAcesso origem = Registro();

        RegistroDeAcesso lido = IdaEVolta(MapeadorDoRegistroDeAcesso.ParaLote([origem])).Registros[0].Registro;

        lido.Instante.Should().Be(origem.Instante.UtcDateTime);
        lido.Instante.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact(DisplayName = "A entrada do lote contém chave e registro")]
    public void EntradaDoLote_ContemChaveERegistro() =>
        EsquemaDoRegistroDeAcesso.Entrada.Fields.Select(static campo => campo.Name).Should().Equal("chave", "registro");

    [Fact(DisplayName = "O subject do esquema é o do tópico com o sufixo do valor")]
    public void Subject_EOTopicoComSufixoDoValor() =>
        EsquemaDoRegistroDeAcesso.SubjectDoTopico("autorizacao_registros_de_acesso").Should().Be("autorizacao_registros_de_acesso-value");

    private static void ConferirRegistro(EntradaDoLote entrada, RegistroDecisaoAcesso origem)
    {
        entrada.Chave.Should().Be(origem.Chave);
        entrada.Registro.Should().BeEquivalentTo(EsperadoDoAcesso(origem), opcoes => opcoes.ExcludingMissingMembers());
        entrada.Registro.Should().BeEquivalentTo(EsperadoDoDesfecho(origem), opcoes => opcoes.ExcludingMissingMembers());
    }

    private static object EsperadoDoAcesso(RegistroDecisaoAcesso origem) =>
        new
        {
            Instante = origem.Instante.UtcDateTime,
            origem.AtorEmissor,
            origem.AtorContaId,
            origem.RequestId,
            ExecucaoId = origem.ExecucaoId.ToString(),
            origem.Permissao,
            origem.ResourceTipo,
            origem.Sensibilidade,
            origem.Origem,
            origem.ClienteOidc,
        };

    private static object EsperadoDoDesfecho(RegistroDecisaoAcesso origem) =>
        new
        {
            Resultado = ValoresCanonicos.De(origem.Resultado),
            origem.Permitido,
            origem.MotivoNegativa,
            origem.FonteGrant,
            ResourceId = origem.ResourceId?.ToString(),
        };

    private static LoteDoRegistroDeAcesso IdaEVolta(LoteDoRegistroDeAcesso lote)
    {
        using MemoryStream fluxo = new();
        new SpecificDatumWriter<LoteDoRegistroDeAcesso>(EsquemaDoRegistroDeAcesso.Lote).Write(lote, new BinaryEncoder(fluxo));
        fluxo.Position = 0;

        return new SpecificDatumReader<LoteDoRegistroDeAcesso>(EsquemaDoRegistroDeAcesso.Lote, EsquemaDoRegistroDeAcesso.Lote)
            .Read(null!, new BinaryDecoder(fluxo));
    }
}
