namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O tipo de documento sob o circuito de aprovação da proteção de dados.</summary>
internal sealed class TipoSobAprovacao(TipoDocumento tipo) : IItemSobAprovacao
{
    public ItemDaProtecao Item => new(CadastroDoCatalogo.TipoDocumento, tipo.Id);

    public ProtecaoDeDados Protecao => tipo.Protecao;

    public AprovacaoDaProtecao Aprovacao => tipo.Aprovacao;

    public Result Solicitar(string ator, DateTimeOffset em) => tipo.SolicitarAprovacaoDaProtecao(ator, em);

    public RetratoDoItemDaProtecao Retrato() =>
        new(Item, tipo.Codigo.Valor, tipo.Nome, tipo.Protecao, tipo.Aprovacao.Ciclo, tipo.Aprovacao.ManifestacaoDesfavoravelVigente);

    public Result Decidir(DecisaoFundamentada decisao, string ator, DateTimeOffset em) =>
        decisao.Ato switch
        {
            AtoDaProtecao.Aprovacao => tipo.AprovarProtecao(ator, em),
            AtoDaProtecao.AjusteRecomendado => tipo.RecomendarAjusteDaProtecao(
                decisao.ClassificacaoRecomendada ?? ClassificacaoDeProtecao.Nenhuma, decisao.Justificativa?.Valor, ator, em),
            _ => tipo.ReprovarProtecao(decisao.Justificativa?.Valor, ator, em),
        };
}
