namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Permissões de um módulo no catálogo.</summary>
/// <param name="Modulo">Módulo.</param>
/// <param name="Resources">Resources do módulo.</param>
public sealed record ModuloDoCatalogoResponse(string Modulo, IReadOnlyList<ResourceDoCatalogoResponse> Resources);

/// <summary>Permissões de um resource no catálogo.</summary>
/// <param name="Resource">Resource.</param>
/// <param name="Permissoes">Permissões do resource.</param>
public sealed record ResourceDoCatalogoResponse(string Resource, IReadOnlyList<PermissaoDoCatalogoResponse> Permissoes);

/// <summary>Uma permissão do catálogo.</summary>
/// <param name="Codigo">Código da permissão.</param>
/// <param name="Nome">Nome legível.</param>
/// <param name="Descricao">Descrição do que autoriza.</param>
/// <param name="Sensibilidade">Sensibilidade do dado (PUBLICO, INTERNO, PESSOAL, IDENTIFICADOR, SENSIVEL).</param>
public sealed record PermissaoDoCatalogoResponse(string Codigo, string Nome, string Descricao, CodigoComRotulo<ClassificacaoDeProtecao> Sensibilidade);

/// <summary>Uma permissão que o usuário logado possui.</summary>
/// <param name="Codigo">Código da permissão.</param>
/// <param name="Nome">Nome legível.</param>
public sealed record PermissaoEfetivaResponse(string Codigo, string Nome);
