namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>Os cadastros do catálogo cujos itens passam pela aprovação da proteção de dados, como saem no corpo das requisições.</summary>
public static class CadastrosDaProtecaoDoCatalogo
{
    public const string TipoDocumento = "TIPO_DOCUMENTO";
    public const string Fato = CadastrosDoCatalogo.DadoDoCandidato;
}
