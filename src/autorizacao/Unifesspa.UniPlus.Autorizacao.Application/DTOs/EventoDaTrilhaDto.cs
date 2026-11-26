namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;

/// <summary>Evento da trilha de uma concessão, em ordem cronológica. É criado pelas fábricas, que dizem quem agiu.</summary>
/// <param name="Tipo">O que aconteceu; as origens da concessão acrescentam os seus.</param>
/// <param name="Autor">Quem praticou o ato; nulo quando o ator é o sistema e não há identidade a expor.</param>
/// <param name="Ator">Se o ato foi de uma pessoa ou do sistema.</param>
public sealed record EventoDaTrilhaDto(
    CodigoComRotulo<TipoDoEventoDoHistoricoDaConcessao> Tipo,
    DateTimeOffset Instante,
    IdentidadeDto? Autor,
    string? Detalhe,
    CodigoComRotulo<AtorDoEventoDoHistoricoDaConcessao> Ator)
{
    /// <summary>O ato de uma pessoa, que sempre tem a identidade de quem agiu.</summary>
    public static EventoDaTrilhaDto DaPessoa(
        TipoDoEventoDoHistoricoDaConcessao tipo, DateTimeOffset instante, IdentidadeDto autor, string? detalhe)
    {
        ArgumentNullException.ThrowIfNull(autor);

        return Criar(tipo, instante, autor, detalhe, AtorDoEventoDoHistoricoDaConcessao.Humano);
    }

    /// <summary>O ato do sistema; o autor só existe quando um componente do sistema, como o verificador, é quem o praticou.</summary>
    public static EventoDaTrilhaDto DoSistema(
        TipoDoEventoDoHistoricoDaConcessao tipo, DateTimeOffset instante, IdentidadeDto? autor, string? detalhe) =>
        Criar(tipo, instante, autor, detalhe, AtorDoEventoDoHistoricoDaConcessao.Sistema);

    private static EventoDaTrilhaDto Criar(
        TipoDoEventoDoHistoricoDaConcessao tipo,
        DateTimeOffset instante,
        IdentidadeDto? autor,
        string? detalhe,
        AtorDoEventoDoHistoricoDaConcessao ator) =>
        new(CodigoComRotulo<TipoDoEventoDoHistoricoDaConcessao>.De(tipo), instante, autor, detalhe, CodigoComRotulo<AtorDoEventoDoHistoricoDaConcessao>.De(ator));
}
