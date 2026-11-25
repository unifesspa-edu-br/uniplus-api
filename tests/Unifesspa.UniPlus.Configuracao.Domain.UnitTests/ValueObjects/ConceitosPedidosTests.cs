namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Os códigos de conceito pedidos para um tipo de etapa, conferidos contra o catálogo.</summary>
public sealed class ConceitosPedidosTests
{
    private const string Campo = "conceitosAdmitidos";

    private static TipoConceito Conceito(string codigo, bool ativo = true)
    {
        TipoConceito tipo = TipoConceito.Criar(codigo, DefinicoesDeTipoConceito.Numerica()).Value!;
        if (!ativo)
        {
            tipo.Desativar();
        }

        return tipo;
    }

    [Fact(DisplayName = "Lista ausente é recusada como campo obrigatório")]
    public void Resolver_ListaAusente_RecusaComoObrigatorio()
    {
        Result<IReadOnlyList<TipoConceito>> resultado = ConceitosPedidos.De(null).Resolver([]);

        resultado.DeveRecusarComo(TipoEtapaErrorCodes.ConceitosAdmitidosObrigatorio, Campo);
    }

    [Fact(DisplayName = "Lista vazia é válida e não admite conceito algum")]
    public void Resolver_ListaVazia_Aceita()
    {
        Result<IReadOnlyList<TipoConceito>> resultado = ConceitosPedidos.De([]).Resolver([]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeEmpty();
    }

    [Fact(DisplayName = "Códigos de conceitos ativos do catálogo resolvem nos próprios tipos")]
    public void Resolver_CodigosAtivos_DevolveOsTipos()
    {
        TipoConceito nota = Conceito("NOTA");
        TipoConceito parecer = Conceito("PARECER");

        Result<IReadOnlyList<TipoConceito>> resultado = ConceitosPedidos.De(["PARECER", "NOTA"]).Resolver([nota, parecer]);

        resultado.Value.Should().BeEquivalentTo([nota, parecer]);
    }

    [Fact(DisplayName = "Código fora do catálogo é recusado como conceito inexistente")]
    public void Resolver_CodigoForaDoCatalogo_RecusaComoInexistente()
    {
        Result<IReadOnlyList<TipoConceito>> resultado = ConceitosPedidos.De(["NOTA", "FANTASMA"]).Resolver([Conceito("NOTA")]);

        resultado.DeveRecusarComo(TipoEtapaErrorCodes.ConceitoInexistente, Campo);
    }

    [Fact(DisplayName = "Conceito desativado é recusado como inativo")]
    public void Resolver_ConceitoDesativado_RecusaComoInativo()
    {
        Result<IReadOnlyList<TipoConceito>> resultado = ConceitosPedidos.De(["ANTIGO"]).Resolver([Conceito("ANTIGO", ativo: false)]);

        resultado.DeveRecusarComo(TipoEtapaErrorCodes.ConceitoInativo, Campo);
    }

    [Fact(DisplayName = "Código repetido é recusado como conceito duplicado")]
    public void Resolver_CodigoRepetido_RecusaComoDuplicado()
    {
        Result<IReadOnlyList<TipoConceito>> resultado = ConceitosPedidos.De(["NOTA", "NOTA"]).Resolver([Conceito("NOTA")]);

        resultado.DeveRecusarComo(TipoEtapaErrorCodes.ConceitoDuplicado, Campo);
        resultado.Errors.Should().ContainSingle();
    }

    [Fact(DisplayName = "Espaços no código não identificam o conceito")]
    public void Resolver_CodigoComEspacos_RecusaComoInexistente()
    {
        Result<IReadOnlyList<TipoConceito>> resultado = ConceitosPedidos.De([" NOTA "]).Resolver([Conceito("NOTA")]);
        resultado.DeveRecusarComo(TipoEtapaErrorCodes.ConceitoInexistente, Campo);
    }

    [Fact(DisplayName = "Inexistente, inativo e duplicado acumulam no mesmo lote, uma recusa por espécie")]
    public void Resolver_VariasEspecies_AcumulaUmaRecusaPorEspecie()
    {
        TipoConceito nota = Conceito("NOTA");
        TipoConceito antigo = Conceito("ANTIGO", ativo: false);

        Result<IReadOnlyList<TipoConceito>> resultado =
            ConceitosPedidos.De(["NOTA", "NOTA", "ANTIGO", "FANTASMA", "OUTRO"]).Resolver([nota, antigo]);

        resultado.Errors.Select(erro => (erro.Field, erro.Error.Code)).Should().Equal(
            (Campo, TipoEtapaErrorCodes.ConceitoInexistente),
            (Campo, TipoEtapaErrorCodes.ConceitoInativo),
            (Campo, TipoEtapaErrorCodes.ConceitoDuplicado));
    }

    [Fact(DisplayName = "A recusa não repete o código informado")]
    public void Resolver_Recusa_NaoEcoaOCodigoInformado()
    {
        Result<IReadOnlyList<TipoConceito>> resultado = ConceitosPedidos.De(["CODIGO_RECUSADO"]).Resolver([]);

        resultado.Errors.Should().OnlyContain(erro => !erro.Error.Message.Contains("CODIGO_RECUSADO", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Código em branco é recusado como conceito inexistente")]
    public void Resolver_CodigoEmBranco_RecusaComoInexistente()
    {
        Result<IReadOnlyList<TipoConceito>> resultado = ConceitosPedidos.De(["  "]).Resolver([]);

        resultado.DeveRecusarComo(TipoEtapaErrorCodes.ConceitoInexistente, Campo);
    }

    [Fact(DisplayName = "A consulta ao catálogo leva apenas códigos válidos e distintos")]
    public void CodigosParaConsulta_TrazOsValidosEDistintos()
    {
        ConceitosPedidos pedidos = ConceitosPedidos.De([" NOTA ", "NOTA", "", "PARECER"]);

        pedidos.CodigosParaConsulta.Should().Equal("NOTA", "PARECER");
    }

    [Fact(DisplayName = "O código com caractere nulo não vai ao banco e é recusado como conceito inexistente")]
    public void CodigoCaractereInvalido_NaoEhConsultadoEEhInexistente()
    {
        ConceitosPedidos pedidos = ConceitosPedidos.De(["NOTA", "NO\0TA"]);

        pedidos.CodigosParaConsulta.Should().Equal("NOTA");
        Result<IReadOnlyList<TipoConceito>> resultado = pedidos.Resolver([]);
        resultado.Errors.Select(static erro => erro.Error.Code).Should().Equal(TipoEtapaErrorCodes.ConceitoInexistente);
    }

    [Fact(DisplayName = "Sem lista não há o que consultar no catálogo")]
    public void CodigosParaConsulta_ListaAusente_EhVazia()
    {
        ConceitosPedidos.De(null).CodigosParaConsulta.Should().BeEmpty();
    }
}
