namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O fato do candidato sob o circuito de aprovação da proteção de dados.</summary>
internal sealed class FatoSobAprovacao(FatoCandidato fato) : IItemSobAprovacao
{
    public ItemDaProtecao Item => new(CadastroDoCatalogo.Fato, fato.Id);

    public ProtecaoDeDados Protecao => fato.Protecao;

    public AprovacaoDaProtecao Aprovacao => fato.Aprovacao;

    public Result Solicitar(string ator, DateTimeOffset em) => fato.SolicitarAprovacaoDaProtecao(ator, em);

    public RetratoDoItemDaProtecao Retrato() =>
        new(Item, fato.Codigo, fato.Nome, fato.Protecao, fato.Aprovacao.Ciclo, fato.Aprovacao.ManifestacaoDesfavoravelVigente);

    public Result Decidir(DecisaoFundamentada decisao, string ator, DateTimeOffset em) =>
        decisao.Ato switch
        {
            AtoDaProtecao.Aprovacao => fato.AprovarProtecao(ator, em),
            AtoDaProtecao.AjusteRecomendado => fato.RecomendarAjusteDaProtecao(
                decisao.ClassificacaoRecomendada ?? ClassificacaoDeProtecao.Nenhuma, decisao.Justificativa?.Valor, ator, em),
            _ => fato.ReprovarProtecao(decisao.Justificativa?.Valor, ator, em),
        };
}
