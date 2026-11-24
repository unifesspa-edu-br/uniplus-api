namespace Unifesspa.UniPlus.Selecao.Application.Services.ListagemDeInscricoes;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A visibilidade decidida para as inscrições da página.</summary>
public sealed class ProjecaoDaListagemDeInscricoes
{
    private readonly DecisoesDaProjecao _decisoes;
    private readonly (NaturezaDoItem, ClassificacaoDeProtecao)[] _classes;
    private readonly LeituraDasVersoes _versoes;
    private readonly NomeClassificadoDto _modeloDoNome;

    internal ProjecaoDaListagemDeInscricoes(DecisoesDaProjecao decisoes,
        (NaturezaDoItem, ClassificacaoDeProtecao)[] classes, LeituraDasVersoes versoes)
    {
        _decisoes = decisoes;
        _classes = classes;
        _versoes = versoes;
        _modeloDoNome = new(versoes.ClassificacaoDoNome, versoes.BaseLegalDoNome) { Valor = string.Empty };
    }

    public bool SaiONome => _versoes.NomeEntraNosPares && _decisoes.ItemSai(_modeloDoNome);
    public bool SaiOCpf => Permite(nameof(InscricaoDoProcessoDto.Cpf));
    internal DecisaoDeAcesso? DecisaoExercida => _classes.Select(par => _decisoes.DecisaoDo(par.Item1, par.Item2))
        .FirstOrDefault(static decisao => decisao is not null);

    public InscricaoDoProcessoDto Projetar(ResumoDaInscricaoDoProcesso inscricao, ProcessoDaInscricaoDto processo)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        return new(
            inscricao.Id, processo, Fixo(nameof(InscricaoDoProcessoDto.NumeroInscricao), inscricao.NumeroInscricao),
            Fixo<SituacaoInscricao>(nameof(InscricaoDoProcessoDto.Situacao), inscricao.Situacao) is { } situacao
                ? CodigoComRotulo<SituacaoInscricao>.De(situacao) : null,
            OfertasVisiveis(inscricao), SaiONome && inscricao.Nome is { } nome ? _modeloDoNome with { Valor = nome } : null,
            SaiOCpf && inscricao.Cpf is { } cpf ? new CpfMascaradoDto(cpf.Mascarado, PodeRevelar: false) : null,
            Fixo<DateTimeOffset>(nameof(InscricaoDoProcessoDto.SubmetidaEm), inscricao.SubmetidaEm));
    }

    private bool Permite(string propriedade) => _decisoes.PermitePropriedade<InscricaoDoProcessoDto>(propriedade);
    private T? Fixo<T>(string propriedade, T? valor) where T : struct => Permite(propriedade) ? valor : null;
    private string? Fixo(string propriedade, string? valor) => Permite(propriedade) ? valor : null;
    private IReadOnlyList<OfertaEscolhidaDto>? OfertasVisiveis(ResumoDaInscricaoDoProcesso inscricao) =>
        Permite(nameof(InscricaoDoProcessoDto.Ofertas))
            ? OfertasEscolhidas.Montar(inscricao.OfertaCursoId, inscricao.OfertaCursoSegundaOpcaoId,
                _versoes.NomesDasOfertasDa(inscricao.VersaoFormularioId)) : null;
}
