namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class MontagemDoLocalDeOferta
{
    internal static readonly DateTimeOffset Agora = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A declaração válida de um polo EaD em Marabá; o teste informa só o que difere dela, com <c>ajuste</c>.
    /// </summary>
    internal static DeclaracaoDoLocalDeOferta Declaracao(Func<DeclaracaoDoLocalDeOferta, DeclaracaoDoLocalDeOferta>? ajuste = null)
    {
        DeclaracaoDoLocalDeOferta declaracao = new(TipoLocalOferta.PoloEad, null, "1504208", "Marabá", "PA", null, null);

        return ajuste?.Invoke(declaracao) ?? declaracao;
    }

    internal static Result<LocalOferta> Criar(Func<DeclaracaoDoLocalDeOferta, DeclaracaoDoLocalDeOferta>? ajuste = null) =>
        LocalOferta.Criar(Declaracao(ajuste), Agora);

    internal static LocalOferta Existente(Func<DeclaracaoDoLocalDeOferta, DeclaracaoDoLocalDeOferta>? ajuste = null) =>
        Criar(ajuste).ValorEsperado();

    internal static ReferenciaEnderecoGeo Endereco(string cidadeCodigoIbge = "1504208", string cidadeUf = "PA") =>
        ReferenciaEnderecoGeo.Criar(
            "68507590", "Folha 31", "s/n", null, "Nova Marabá", null,
            cidadeCodigoIbge, "Marabá", cidadeUf, -5.3m, -49.1m,
            NivelResolucaoEndereco.Logradouro, "logradouro", Agora).ValorEsperado();
}
