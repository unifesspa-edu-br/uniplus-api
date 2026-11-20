namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi.Apoio;

using System.ComponentModel;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Os enums e os tipos de pedido, de consulta e de resposta que os testes do contrato dos códigos publicam.</summary>
public static class ModeloDeCodigosDeTeste
{
    [DescricaoDoContrato("Em que fase o processo está, para todos os códigos.")]
    public enum Fase
    {
        [CodigoDeContrato("inscricao", "Inscrição", Descricao = "A pessoa ainda pode se inscrever.")]
        Inscricao = 1,

        [CodigoDeContrato("resultado", "Resultado", Descricao = "O resultado já saiu.")]
        Resultado = 2,
    }

    public enum Turno
    {
        [CodigoDeContrato("manha", "Manhã", Descricao = "Aulas de manhã.")]
        Manha = 1,

        [CodigoDeContrato("noite", "Noite")]
        Noite = 2,
    }

    public sealed record RespostaDeFase(CodigoComRotulo<Fase> Fase, CodigoComDescricao<Fase> FaseComDescricao);

    public sealed class PedidoDeFase
    {
        [AceitaCodigosDe<Fase>]
        public required string Fase { get; init; }

        [AceitaCodigosDe<Fase>]
        public string? FaseOpcional { get; init; }

        [AceitaCodigosDe<Fase>]
        public IReadOnlyList<string?>? Fases { get; init; }

        [AceitaCodigosDe<Fase>]
        public required string[] FasesObrigatorias { get; init; }

        [AceitaCodigosDe<Fase>]
        [Description("A fase em que o processo está.")]
        public required string FaseDescrita { get; init; }

        [AceitaCodigosDe<Fase>]
        [Description("A fase que o pedido muda, se mudar.")]
        public string? FaseOpcionalDescrita { get; init; }

        public string? Livre { get; init; }
    }

    public sealed record PedidoPosicional(
        [AceitaCodigosDe<Fase>] string NoParametro,
        [property: AceitaCodigosDe<Fase>] IReadOnlyList<string?>? NaPropriedade);

    /// <summary>O construtor que o serializador usa marca o parâmetro; a outra sobrecarga não.</summary>
    public sealed class PedidoComConstrutorMarcado
    {
        [JsonConstructor]
        public PedidoComConstrutorMarcado([AceitaCodigosDe<Fase>] string fase, string? livre)
        {
            Fase = fase;
            Livre = livre;
        }

        public PedidoComConstrutorMarcado(string fase)
            : this(fase, null)
        {
        }

        public string Fase { get; }

        public string? Livre { get; }
    }

    /// <summary>O construtor que o serializador usa não marca o parâmetro; outra sobrecarga marca o de mesmo nome.</summary>
    public sealed class PedidoComOutraSobrecargaMarcada
    {
        [JsonConstructor]
        public PedidoComOutraSobrecargaMarcada(string fase, string? livre)
        {
            Fase = fase;
            Livre = livre;
        }

        public PedidoComOutraSobrecargaMarcada([AceitaCodigosDe<Fase>] string fase)
            : this(fase, null)
        {
        }

        public string Fase { get; }

        public string? Livre { get; }
    }

    public sealed class FiltroDeFase
    {
        [FromQuery(Name = "faseDoFiltro")]
        [AceitaCodigosDe<Fase>]
        public string? Fase { get; init; }

        [FromQuery(Name = "livreDoFiltro")]
        public string? Livre { get; init; }
    }

    public sealed class PedidoComNumeroMarcado
    {
        [AceitaCodigosDe<Fase>]
        public int Quantidade { get; init; }
    }
}
