# SolidWorks API 摘要

本文档按功能分类提供了 SolidWorks API 接口的摘要说明。

## 应用程序接口

### IAssemblyDoc
允许访问执行装配体操作的函数；例如，添加新组件、添加配合条件、隐藏和分解组件。

### IDrawingDoc
允许访问执行工程图操作的函数。

### IModelDoc2
允许访问 SOLIDWORKS 文档：零件、装配体和工程图。

### IModelDocExtension
允许访问模型。

### IPartDoc
提供对在零件文档中执行零件操作的函数的访问。

### ISldWorks
提供对 SOLIDWORKS API 中公开的所有其他接口的直接和间接访问。

## 注解接口

### IAnnotation
允许访问注释、焊接符号、基准标记、显示尺寸、块、装饰螺纹、中心标记、中心线和其他注解类型。

### IAutoBalloonOptions
允许访问自动球标选项。

### IBalloonOptions
允许访问球标选项。

### IBalloonStack
允许访问应用于球标堆栈的属性，例如堆栈的方向。

### ICenterLine
允许访问中心线。

### ICenterMark
允许访问工程图视图中的中心标记或中心标记集。

### ICThread
允许访问装饰螺纹。

### IDatumOrigin
允许访问基准原点注解。

### IDatumTag
允许访问基准标记的显示信息。

### IDatumTargetSym
允许访问基准目标符号注解的显示信息。

### IDimensionTolerance
允许您获取和设置尺寸公差。

### IDisplayDimension
表示在零件、装配体、工程图和传感器中显示的尺寸实例。

### ICalloutAngleVariable
允许访问孔标注中的角度变量。

### ICalloutLengthVariable
允许访问孔标注中的长度变量。

### ICalloutStringVariable
允许访问孔标注中的字符串变量。

### ICalloutVariable
允许访问孔标注。

### IDowelSymbol
允许访问定位销符号。

### IGtol
允许您获取和设置几何公差 (GTol) 参数。

### IGtolFrame
允许访问 Gtol 特征控制框中符号的指示符和 XML 字符串。

### IMagneticLine
允许访问磁力线。

### IMultiJogLeader
允许访问多折线引线的显示信息。

### INote
允许您获取标准注释信息。

### IParagraphs
允许访问注释注解中的段落。

### IPMIDatumData
允许访问 STEP 242 模型中基准注解的产品制造信息 (PMI)。

### IPMIDatumFeature
允许访问产品制造信息 (PMI) 基准特征。

### IPMIDatumTarget
允许访问产品制造信息 (PMI) 基准目标。

### IPMIDimensionData
允许访问 STEP 242 模型中尺寸注解的产品制造信息 (PMI)。

### IPMIDimensionItem
允许访问产品制造信息 (PMI) 尺寸项。

### IPMIFrameData
允许访问产品制造信息 (PMI) Gtol 框架。

### IPMIGtolBoxData
允许访问产品制造信息 (PMI) Gtol 公差框。

### IPMIGtolData
允许访问 STEP 242 模型中 Gtol 注解的产品制造信息 (PMI)。

### IPMIGtolFrameDatum
允许访问产品制造信息 (PMI) Gtol 基准框。

### IRevisionCloud
允许访问修订云线注解。

### ISFSymbol
允许访问表面光洁度符号的显示信息。

### ISketchBlockDefinition
允许访问块定义的信息。

### ISketchBlockInstance
允许访问块实例。

### IStackedBalloonOptions
允许访问堆叠球标选项。

### IBendTableAnnotation
允许访问折弯表注解。

### IBomTableAnnotation
允许访问此表格注解的 IBomFeature 对象。

### IGeneralTableAnnotation
允许访问通用表格注解。

### IGeneralToleranceTableAnnotation
允许访问通用公差表注解。

### IHoleTableAnnotation
访问孔表注解。

### IPunchTableAnnotation
允许访问冲压表注解。

### IRevisionTableAnnotation
访问修订表注解。

### ITableAnnotation
提供对表格注解的访问。

### ITitleBlockTableAnnotation
提供对标题栏表格注解的访问。

### IWeldmentCutListAnnotation
允许访问焊件切割清单表中的注解。

### IWeldBead
允许访问焊缝注解。

### IWeldSymbol
允许访问焊接符号。

## 装配体接口

### IAdvancedSelectionCriteria
Allows access to advanced component selection criteria for an assembly.

### ICollision
Allows access to collision data.

### ICollisionDetectionGroup
Allows access to collision detection groups.

### ICollisionDetectionManager
Allows access to the collision detection manager.

### IComponent2
Allows access to the components within assemblies.

### IDragOperator
Allows access to settings for the Move Components command in the SOLIDWORKS user-interface.

### IExplodeStep
Allows access to an explode step in the explode view of the active assembly configuration.

### IGroundPlaneFeatureData
Allows access to a ground plane feature.

### IInterference
Allows access to the components that interfere when interference detection is calculated.

### IInterferenceDetectionMgr
Allows you to run interference detection on an assembly to determine whether components interfere with each other.

### IMirrorComponentFeatureData
Allows access to a mirror component feature.

### IAngleMateFeatureData
Allows access to an angle mate or a limit angle mate feature.

### ICamFollowerMateFeatureData
Allows access to a cam-follower mate feature.

### ICoincidentMateFeatureData
Allows access to a coincident mate feature.

### IConcentricMateFeatureData
Allows access to a concentric mate feature.

### IDistanceMateFeatureData
文件未找到。

### IGearMateFeatureData
Allows access to gear mate features.

### IHingeMateFeatureData
Allows access to a hinge mate feature.

### ILinearCouplerMateFeatureData
Allows access to linear/linear coupler mate feature data.

### ILockMateFeatureData
Allows access to lock mate features.

### IMate2
Allows access to various assembly mate parameters.

### IMateEntity2
Allows access to mated entities and the assembly mate definition.

### IMateFeatureData
Allows access to mate feature data.

### IMateInPlace
Allows access to an Inplace (coincident) mate, which is created when you insert a component in the context of an assembly.

### IMateLoadReference
Allows access to mate load references.

### IMateReference
Allows access to a mate reference, which specifies one or more entities of a component to use for automatic mating.

### IParallelMateFeatureData
Allows access to a parallel mate feature.

### IPerpendicularMateFeatureData
Allows access to a perpendicular mate feature.

### IProfileCenterMateFeatureData
Allows access to profile center mate feature data.

### IRackPinionMateFeatureData
Allows access to a rack and pinion mate feature.

### IScrewMateFeatureData
Allows access to a screw mate feature.

### ISlotMateFeatureData
Allows access to a slot mate feature.

### ISymmetricMateFeatureData
Allows access to symmetry mate feature data.

### ITangentMateFeatureData
Allows access to a tangent mate feature.

### IUniversalJointMateFeatureData
Allows access to a universal joint mate feature.

### IWidthMateFeatureData
Allows access to width mate feature data.

## 工程图接口

### IBreakLine
Allows access to information about a break line in a drawing view.

### ICenterOfMass
Allows access to the centers of mass in a drawing view.

### IDetailCircle
Allows access to a detail circle.

### IDrawingComponent
Represents the referenced component in a drawing view.

### IDrSection
Allows access to a section view in a drawing.

### IFaceHatch
Represents a cross-hatch, which is automatically added by SOLIDWORKS when you create a section view, aligned section view, break view, or detail view.

### ILayer
Allows access to the properties and items on a layer, including the color, width, name, etc., used to define the layer.

### ILayerMgr
Allows you to manage a drawing document's layers.

### IProjectionArrow
Allows access to a projection arrow.

### ISheet
Allows access to a sheet and objects on the sheet such as BOM tables.

### ISilhouetteEdge
Allows you to access silhouette edges in drawing documents.

### IBendTable
Allows access to a bend table feature.

### IBomFeature
Allows access to the BOM table feature.

### IBomTable
Allows access to BOM table information and values. IMPORTANT: You can no longer insert IBomTable objects; you can now only insert IBomTableAnnotation objects. IBomTable objects are not and cannot be converted to IBomTableAnnotation objects. Use the IBomTable APIs for legacy BOM tables only.

### IGeneralTableFeature
Allows access to the general table feature.

### IGeneralToleranceTableFeature
Allows access to the general tolerance table feature.

### IHoleTable
Allows access to a hole table feature in a drawing document.

### IPunchTable
Allows access to punch table information and values.

### IRevisionTableFeature
Allows access to the revision table feature.

### ITitleBlockTableFeature
Provides access to title block table features.

### ITableAnchor
Allows access to the data that defines a table anchor feature.

### ITitleBlock
Allows access to the title block in this sheet.

### IView
Represents drawing views found in a drawing document.

### IView3D
Allows access to a 3D View of a part or assembly.

## 配置接口

### IConfiguration
Allows you to manage different part or assembly states.

### IConfigurationManager
Allows access to a configuration in a model.

### IDesignTable
Allows access to design table information and values.

### IPartExplodeStep
Allows access to the explode step of an explode view of a multibody part.

## 枚举接口

### IEnumBodies2
Allows access to bodies enumeration.

### IEnumCoEdges
Allows access to a coedges enumeration.

### IEnumComponents2
Allows access to a components enumeration.

### IEnumDisplayDimensions
Allows access to a display dimensions enumeration.

### IEnumDocuments2
Allows access to a documents enumeration.

### IEnumDrSections
Allows access to a section views enumeration.

### IEnumEdges
Allows access to an edges enumeration.

### IEnumFaces2
Allows access to a faces enumeration.

### IEnumLoops2
Allows access to a loops enumeration.

### IEnumModelViews
Allows access to a model views enumeration.

### IEnumSketchHatches
Allows access to a sketch hatches enumeration.

### IEnumSketchPoints
Allows access to a sketch points enumeration.

### IEnumSketchSegments
Allows access to the sketch segments enumeration.

## 特征接口

### IBeltChainFeatureData
Allows access to a Belt/Chain assembly feature.

### IFeature
Allows access to the feature type, name, parameter data, and the next feature in the FeatureManager design tree.

### IFeatureManager
Allows you to create features.

### IChamferFeatureData2
Allows access to a chamfer feature.

### IPartialEdgeFilletData
Allows access to partial fillet/chamfer properties.

### ISimpleFilletFeatureData2
Allows access to a simple fillet feature.

### IVariableFilletFeatureData2
Allows access to a variable radius fillet feature.

### IBodyFolder
Allows access to the bodies in solid, surface, and various weldment folders.

### IComment
Allows access to the comments in the Comment folder in the FeatureManager design tree.

### ICommentFolder
Allows access to the Comment folder in the FeatureManager design tree.

### ICosmeticWeldBeadFolder
Allows access to the properties of cosmetic weld beads.

### IFeatureFolder
Allows access to the contents of feature folders in the FeatureManager design tree.

### IFlatPatternFolder
Allows access to the flat-pattern folder in the FeatureManager design tree.

### ISheetMetalFolder
Allows access to a sheet metal folder feature in the FeatureManager design tree.

### IAdvancedHoleElementData
Allows access to Advanced Hole element data.

### IAdvancedHoleFeatureData
Allows access to the Advanced Hole feature data.

### ICounterboreElementData
Allows access to the data of a counterbore hole element in an Advanced Hole.

### ICountersinkElementData
Allows access to the data of a countersink hole element in an Advanced Hole.

### IHoleSeriesFeatureData2
Allows access to the data that defines a hole series feature.

### ISimpleHoleFeatureData2
Allows access to a simple hole feature.

### IStraightElementData
Allows access to the data of a simple hole element in an Advanced Hole.

### IStraightTapElementData
Allows access to the data of a straight tap hole element in an Advanced Hole.

### ITaperedTapElementData
Allows access to the data of a tapered tap hole element in an Advanced Hole.

### IWizardHoleFeatureData2
Allows access to Hole Wizard hole or slot feature data.

### ICamera
Allows access to the camera feature.

### ILight
Allows access to the light feature.

### ICavityFeatureData
Allows access to a cavity feature.

### ICoreFeatureData
Allows access to a core feature.

### IMoveFaceFeatureData
Allows access to Move Face features.

### IPartingLineFeatureData
Allows access to a parting line feature.

### IPartingSurfaceFeatureData
Allows access to a parting surface feature.

### IRuledSurfaceFeatureData
Allows access to a ruled-surface feature.

### IShutOffSurfaceFeatureData
Allows access to a shut-off surface feature.

### IToolingSplitFeatureData
Allows access to a tooling-split feature.

### IChainPatternFeatureData
Allows access to a chain component pattern feature.

### ICircularPatternFeatureData
Allows access to a circular pattern feature.

### ICurveDrivenPatternFeatureData
Allows access to a curve-driven pattern feature.

### IDerivedPatternFeatureData
Allows access to a derived pattern feature.

### IDimPatternFeatureData
Allows access to a variable pattern feature, which uses a table to store the dimensions and values of the pattern instances.

### IFillPatternFeatureData
Allows access to a fill pattern feature.

### IInstanceToVaryOptions
Allows access to options for varying the dimensions and locations of instances in patterns for parts.

### ILinearPatternFeatureData
Allows access to a linear pattern feature.

### ILocalCircularPatternFeatureData
Allows access to a circular component pattern feature in an assembly.

### ILocalCurvePatternFeatureData
Allows access to a curve-driven component pattern feature in an assembly.

### ILocalLinearPatternFeatureData
Allows access to a linear component pattern feature in an assembly.

### ILocalSketchPatternFeatureData
Allows access to a sketch-driven component pattern feature in an assembly.

### IMirrorPartFeatureData
Allows access to a mirror part feature.

### IMirrorPatternFeatureData
Allows access to a mirror pattern feature.

### IMirrorSolidFeatureData
Allows access to a mirror solid feature.

### ISketchPatternFeatureData
Allows access to a sketch-driven pattern feature in a part.

### ITablePatternFeatureData
Allows access to a table-driven pattern feature.

### IBoundingBoxFeatureData
Allows access to a bounding box feature.

### ICompositeCurveFeatureData
Allows access to a composite curve feature.

### IFreePointCurveFeatureData
Allows access to a curve created using X, Y, Z coordinates for the points.

### IHelixFeatureData
Allows access to a helix feature.

### IImportedCurveFeatureData
Allows access to an imported curve feature.

### IProjectionCurveFeatureData
Allows access to a projection curve feature.

### IRefAxis
Allows access to reference axis definitions.

### IRefAxisFeatureData
Allows access to reference axis feature data.

### IReferenceCurve
Allows access to reference curves.

### IReferencePointCurveFeatureData
Allows access to reference-point curve feature data.

### IRefPlane
Allows access to reference plane definitions.

### IRefPlaneFeatureData
Allows access to reference plane feature data.

### IRefPoint
Allows access to reference points.

### IRefPointFeatureData
Allows access to reference point feature data.

### ISplitLineFeatureData
Allows access to a split line feature.

### IBaseFlangeFeaturedata
Allows access to a sheet metal base flange feature.

### IBendsFeatureData
Allows access to a Flatten-Bends/Process-Bends feature.

### IBreakCornerFeatureData
Allows access to a break corner feature.

### IClosedCornerFeatureData
Allows access to a closed corner feature.

### ICornerReliefFeatureData
Allows access to sheet metal corner relief feature data.

### ICrossBreakFeatureData
Gets or sets cross break feature data.

### ICustomBendAllowance
Allows access to the custom bend allowance of a feature.

### IEdgeFlangeFeatureData
Allows access to a sheet metal edge flange feature.

### IFlatPatternFeatureData
Allows access to a Flat-Pattern feature.

### IFoldsFeatureData
Allows access to a fold feature.

### IHemFeatureData
Allows access to a hem feature.

### IJogFeatureData
Allows access to a jog feature.

### ILoftedBendsFeatureData
Allows access to display information for a lofted bends feature.

### IMiterFlangeFeatureData
Allows access to a miter flange feature.

### IOneBendFeatureData
Allows access to a bend feature (sharp bend, round bend, or flat bend).

### IRipFeatureData
Allows access to a rip feature.

### ISheetMetalFeatureData
Allows access to a sheet metal feature.

### ISheetMetalGaugeTableParameters
Allows access to sheet metal gauge table parameters.

### ISketchedBendFeatureData
Allows access to a sheet metal sketched bend feature.

### ISMCornerReliefData
Allows access to sheet metal corner relief data.

### ISMGussetFeatureData
Allows access to a sheet metal gusset feature.

### ISMNormalCutFeatureData2
Allows access to a sheet metal normal cut feature.

### ISMNormalCutGroupData
Allows access to a sheet metal normal cut feature's cut-extrude face group.

### ISweptFlangeFeatureData
Allows access to a sheet metal swept flange feature.

### ISmartComponentFeatureData
Allows access to a Smart Component.

### IComplexCornerTreatmentFeatureData
Allows access to a complex corner treatment feature of a structure system.

### ICornerManagementFolder
Allows access to a corner management folder in a structure system.

### ICornerMember
Allows access to a corner member of a complex or two member structure system corner treatment feature.

### ICornerTreatmentFeatureData
Allows access to a corner treatment feature of a structure system.

### ICornerTreatmentGroupFolder
Allows access to a corner treatment group folder of a structure system.

### IPrimaryMemberFacePlaneIntersectionFeatureData
Allows access to a structure system member created along the intersection of a face or surface with a plane.

### IPrimaryMemberPathSegmentFeatureData
Allows access to a structure system member created along path segments in a sketch.

### IPrimaryMemberPointLengthFeatureData
Allows access to a structure system member originating at a point and extending to a specified end condition.

### IPrimaryMemberRefPlaneFeatureData
Allows access to a structure system member created along the intersection of two or more planes.

### IPrimaryStructuralMemberFeatureData
Allows access to a primary structure system member.

### IProfileGroupFolder
Allows access to a structure system profile group folder.

### ISecondaryMemberBetweenPointsFeatureData
Allows access to a secondary structure system member that is created between the end points on primary structure system member pairs.

### ISecondaryMemberSupportPlaneFeatureData
Allows access to a secondary structure system member that is created on a plane between primary structure system member pairs.

### ISecondaryMemberUpToMembersFeatureData
Allows access to a secondary structure system up-to member that is created between a selected point and one or more primary structure system members or between one or more point-member pairs.

### ISecondaryStructuralMemberFeatureData
Allows access to a secondary structure system member.

### ISimpleCornerTreatmentFeatureData
Allows access to a simple corner treatment feature of a structure system.

### IStructureSystemFolder
Allows access to a structure system folder.

### IStructureSystemMemberFeatureData
Allows access to a structure system member.

### IStructureSystemMemberProfile
Allows access to a structure system member profile.

### IStructureSystemSplitMember
Allows access to a structure system split member.

### ITwoMemberCornerTreatmentFeatureData
Allows access to a two member corner treatment feature of a structure system.

### IDeleteFaceFeatureData
Allows access to a DeleteFace feature.

### IFillSurfaceFeatureData
Allows access to a fill-surface feature.

### IReplaceFaceFeatureData
Allows access to Replace Face feature data.

### ISurfaceCutFeatureData
Allows access to a surface-cut feature.

### ISurfaceExtendFeatureData
Allows access to a surface-extend feature.

### ISurfaceFlattenFeatureData
Allows access to a surface-flatten feature.

### ISurfaceKnitFeatureData
Allows access to a Surface-Knit feature.

### ISurfaceOffsetFeatureData
Allows access to a surface offset feature.

### ISurfacePlanarFeatureData
Allows access to a planar surface feature.

### ISurfaceRadiateFeatureData
Allows access to a surface radiate feature.

### ISurfaceTrimFeatureData
Allows access to a surface trim feature.

### ISurfExtrudeFeatureData
Allows access to an extruded surface feature.

### ISurfRevolveFeatureData
Allows access to a surface revolve feature.

### IThickenFeatureData
Allows access to a thicken feature.

### IThreadFeatureData
Allows access to a thread feature.

### IBrokenOutSectionFeatureData
Allows access to the broken-out section feature data of a drawing view.

### IEndCapFeatureData
Allows access to an end-cap feature.

### IGussetFeatureData
Allows access to a weldment gusset feature.

### IStructuralMemberFeatureData
Allows access to a structural member.

### IStructuralMemberGroup
Allows access to a weldment structural-member group.

### IWeldmentBeadFeatureData
Allows access to a weldment bead feature.

### IWeldmentCutListFeature
Allows access to a weldment cut list feature.

### IWeldmentTrimExtendFeatureData
Allows access to the data that defines a weldment trim extend feature.

### IBoundaryBossFeatureData
Allows access to a boundary feature that is a boss or base.

### ICombineBodiesFeatureData
Allows access to a combine feature.

### ICoordinateSystemFeatureData
Allows access to a coordinate system feature.

### ICosmeticThreadFeatureData
Allows access to a cosmetic thread feature.

### ICosmeticWeldBeadFeatureData
Allows access to a cosmetic weld bead feature.

### IDeleteBodyFeatureData
Allows access to a Body-Delete/Keep feature.

### IDerivedPartFeatureData
Allows access to a derived part feature.

### IDomeFeatureData2
Allows access to a dome feature.

### IDraftFeatureData2
Allows access to a draft feature.

### IExtrudeFeatureData2
Allows access to an extrusion feature.

### IFeatureStatistics
Allows access to the feature statistics in a part document.

### IHealEdgesFeatureData
Allows access to a heal edges feature.

### IIndentFeatureData
Allows access to an indent feature.

### IIntersectFeatureData
Allows access to an intersect feature.

### IJoinFeatureData
Allows access to a join feature.

### ILibraryFeatureData
Allows access to library feature data.

### ILoftFeatureData
Allows access to a loft feature.

### IMacroFeatureData
Allows access to the data that defines a macro feature.

### IMoveCopyBodyFeatureData
Allows access to a move/copy body feature.

### IRevolveFeatureData2
Allows access to a revolve feature.

### IRibFeatureData2
Allows access to a rib feature.

### ISaveBodyFeatureData
Allows access to a Save Bodies feature.

### IScaleFeatureData
Allows access to a scale feature.

### IShellFeatureData
Allows access to a shell feature.

### ISketchPicture
Provides access to pictures on sketches (i.e., .bmp, .gif, .jpg, .jpeg, .tif, and .wmf).

### ISplitBodyFeatureData
Allows access to a Split feature.

### ISweepFeatureData
Allows access to a sweep feature.

### ITabAndSlotFeatureData
Allows access to a tab and slot feature.

### ITabAndSlotGroupData
Allows access to a tab and slot feature group.

### IWrapSketchFeatureData
Allows access to a wrap feature.

## 模型接口

### IAnnotationView
Allows access to annotation views in parts and assemblies.

### IAttribute
Allows access to an attribute's values.

### IAttributeDef
Allows access to an attribute definition.

### IBody2
Allows access to the faces on a body and the ability to create surfaces for sewing into a body object.

### ICoEdge
Allows access to the underlying edge and loop as well as various coedge data.

### ICurve
Allows access to a curve and its parameters in their native form or in terms of b-curve data.

### IDimension
Allows you to get and set dimension values and tolerances.

### IEdge
Allows access to its defining coedge, and adjacent faces, and its underlying curve and vertices as well as edge data.

### IEdgePoint
Allows access to a midpoint on an edge or an endpoint or midpoint on a reference curve.

### IEntity
Allows access to an attribute instance that is stored on an entity.

### IFace2
Allows access to the underlying edge, loop, and surface to the owning body or feature, and to face tessellation, trim data.

### IFacet
Allows access to a triangular facet of a mesh or graphics body.

### IGraphicsBody
Allows access to a graphics body.

### ILoop2
Allows access to the owning face and to the list of edges and coedges contained in the loop.

### IMeshBody
Allows access to a mesh body.

### IMidSurface3
Allows access to a midsurface feature.

### IModeler
Provides an interface to temporary body objects.

### IParameter
Allows you to get and set values in an attribute.

### ISectionViewData
Allows you to create and access section views in parts and assemblies.

### ISpring
Allows access to the geometry of a spring.

### ISurface
Used as the underlying definition of a face.

### IVertex
Represents the start or end of an edge.

## 运动仿真

### IAVIParameter
（外部 CHM 参考文档）

### ICosmosMotionStudyProperties
（外部 CHM 参考文档）

### ICosmosMotionStudyResults
（外部 CHM 参考文档）

### IMotionPlotAxisFeatureData
Allows access to a plot's x- and y-axis feature data.

### IMotionPlotFeatureData
Allows access to a plot's feature data.

### IMotionPlotFeatureOutput
（外部 CHM 参考文档）

### IMotionStudy
（外部 CHM 参考文档）

### IMotionStudyManager
（外部 CHM 参考文档）

### IMotionStudyProperties
（外部 CHM 参考文档）

### IMotionStudyResults
（外部 CHM 参考文档）

### IPhysicalSimulationMotionStudyProperties
（外部 CHM 参考文档）

### ISimulation3DContactFeatureData
Allows access to a 3D Contact feature in SOLIDWORKS Motion studies.

### ISimulationDamperFeatureData
Allows access to a damper feature in SOLIDWORKS Motion studies.

### ISimulationForceFeatureData
Allows access to a force or torque feature in SOLIDWORKS Motion studies.

### ISimulationGravityFeatureData
Allows access to a gravity feature in SOLIDWORKS Motion studies.

### ISimulationMotorFeatureData
Allows access to the data that defines linear or rotary motors in SOLIDWORKS Motion studies.

### ISimulationSpringFeatureData
Allows access to the data that defines a simulation spring feature in SOLIDWORKS Motion studies.

## 草图接口

### ISketch
Allows access to sketch entities and to extract information about sketch elements, the sketch orientation, and so on.

### ISketchArc
Provides access to properties and methods for sketched arc entities.

### ISketchBlockDefinition
允许访问块定义的信息。

### ISketchBlockInstance
允许访问块实例。

### ISketchContour
Provides access to sketch contours.

### ISketchEllipse
Provides access to sketched ellipse entities.

### ISketchHatch
Represents an area hatch, which is inserted into a SOLIDWORKS drawing polygon or component face when you click Insert &gt; Annotations &gt; Area Hatch/Fill in a SOLIDWORKS drawing.

### ISketchLine
Provides access to sketched line entities.

### ISketchManager
Provides access to sketch-creation routines.

### ISketchParabola
Provides access to sketched parabolas.

### ISketchPath
Provides access to the methods and properties for paths in sketches.

### ISketchPoint
Provides access to sketch point entities.

### ISketchRegion
Provides access to sketch regions.

### ISketchRelation
Provides access to the entities for a sketch relation.

### ISketchRelationManager
Provides access to all sketch relations.

### ISketchSegment
Provides access to functions that are common among sketch entities.

### ISketchSlot
Accesses a sketch slot.

### ISketchSpline
Provides access to sketched spline entities.

### ISketchText
Provides access to sketched text entities.

### ISplineHandle
Provides access to spline handles.

## 实用工具接口

### IBomTableSortData
Allows access to the sort data of an IBomTableAnnotation.

### IBSurfParamData
Allows access to the parameterization data of a B-spline surface.

### IColorTable
Allows access to the color definitions used in SOLIDWORKS.

### ICurveParamData
Allows access to curve parameterization data.

### ICustomPropertyManager
Allows access to the custom properties.

### ICutListItem
Allows access to a configuration-specific cut list folder.

### ICutListSortOptions
Allows access to cut list sorting options.

### IDiagnoseResult
Get the gaps and coedges in each gap on this body.

### IDisplayData
Allows access to display information for certain items, including reference planes and reference axes shown in a drawing view.

### IDocumentSpecification
Allows you to specify how to open a model document. Use this interface's properties before calling ISldWorks::OpenDoc7 to specify how you want to open a model document.

### IEnvironment
Allows you to analyze the text and geometry used to create a geometric tolerance symbol.

### IEquationMgr
Maintains a list of all of the existing equations in a model.

### IExportPdfData
Allows access to the PDF export data interface, which allows you to save: one or more drawing sheets to PDF. parts and assemblies to 3D PDF.

### IFaultEntity
Identifies entities with faults and types of faults.

### IHoleDataTable
Allows access to Hole Wizard fastener tables.

### IHoleStandardsData
Allows access to Hole Wizard standards data.

### IImport3DInterconnectData
Allows access to 3D Interconnect feature data.

### IImportDxfDwgData
Allows you to specify values when importing or inserting DXF/DWG data.

### IImportIgesData
Allows you to specify levels and values when importing IGES data.

### IImportStepData
Allows you to specify values when importing STEP data.

### IManipulator
Allows access to a manipulator, which is similar to the triad or the drag arrow (also called a handle), in a SOLIDWORKS part or assembly document.

### IMassProperty2
Allows access to individual mass properties as found in the Mass Properties dialog box.

### IMassPropertyOverrideOptions
Allows access to mass property override options.

### IMathPoint
Provides a simplified interface for manipulating math-point objects' data and ways to create other math objects.

### IMathTransform
Provides a simplified interface for manipulating transformation matrix data.

### IMathUtility
Provides access to the SOLIDWORKS math objects. These objects can simplify commonly used math calculations used with many API functions.

### IMathVector
Provides a simplified interface for manipulating math vectors.

### IMeasure
Allows access to the measure tool.

### IPageSetup
Allows access to a number of properties related to printer and page setup, including page header and footer information.

### IPLMObjectSpecification
Allows access to a SOLIDWORKS Connected document specification.

### IPrintSpecification
Allows access to a document's printing specification.

### ISafeArrayUtility
Access the ISafeArrayUtility interface, which can: get an unpacked array of native SOLIDWORKS Dispatch-based objects of the same data type and return a packed Variant SafeArray to use in methods that requires passing a packed Variant SafeArray. get a packed Variant SafeArray and return an unpacked array of native SOLIDWORKS Dispatch-based objects of the same data type. get a Variant SafeArray and return the number of SafeArray objects in the Variant SafeArray and their data type. get and put a value in a Variant SafeArray of the same data type.

### ISaveTo3DExperienceOptions
Allows access to options for saving a document in SOLIDWORKS Connected.

### IScanto3D
（外部 CHM 参考文档）

### ISelectData
Allows you to select objects.

### ISelectionMgr
Allows you to get information about selected objects, obtain API objects representing the selected item, and get your selection coordinates interpreted in model or sketch space.

### ISlicingData
Allows access to slicing tool data.

### ISOLIDWORKS Design Checker
（外部 CHM 参考文档）

### ISplineParamData
Allows access to the parameterization data of a spline.

### ISurfaceParameterizationData
Allows access to the parameterization data of a surface.

### ISwOLEObject
Allows access to an OLE object.

### ITessellation
Used to gather tessellation information from a SOLIDWORKS body.

### ITextFormat
Allows access to and control of the formatting of text used in annotations.

### ITexture
Use to apply 2D textures to part and assembly documents for a more realistic finish.

### IUserUnit
Allows you to manage units.

## 用户界面接口

### IAppearanceSetting
Allows access to visual property settings.

### IDecal
Allows access to the decals in models.

### IFaceDecalProperties
Allow access to the properties of decals applied to faces in models.

### IRayTraceRenderer
Allows access to ray-trace rendering engines.

### IRayTraceRendererOptions
Allows access to a ray-trace rendering engine's options.

### IRenderMaterial
Use to apply appearances to models. NOTE: In SOLIDWORKS 2008 and later, materials are called appearances. RealView Graphics must be enabled to see any applied appearances.

### ICallout
Allows add-in applications to manipulate single and multi-row callouts.

### ICommandGroup
Allows add-in applications to create toolbars and menu items, including flyout toolbars and submenus, and add them to the ICommandManager.

### ICommandManager
Allows add-in applications to add and remove CommandGroups (menus and toolbars) to the CommandManager.

### ICommandTab
Allows add-in applications to create tabs and add them to the CommandManager. The add-in application must create and clean up its own tabs.

### ICommandTabBox
Allows add-in applications to create CommandManager tab boxes and add them to a CommandManager tab. The add-in application must create and clean-up its own tab boxes.

### IFlyoutGroup
Allows access to a flyout menu.

### IDisplayStateSetting
Allows access to display state settings.

### IDragArrowManipulator
Allows access to drag arrows, which are called handles in the SOLIDWORKS user interface.

### IFeatMgrView
Allows access to a view (tab) in the FeatureManager design tree.

### IFrame
Allows access to SOLIDWORKS frames, including model windows, menus, and toolbars.

### ILightDialog
Obtained from a LightingDialogCreateNotify event.

### IMaterialVisualPropertiesData
Allows access to a material on a part.

### IMessageBarDefinition
Allows access to a message bar in an add-in.

### IModelView
Allows you access to the model view's orientation, translation, and the Microsoft handle to the window.

### IModelViewManager
Allows access to the model view.

### IModelWindow
Allows access to SOLIDWORKS model windows.

### IMouse
Allows access to the mouse in a model view.

### IPackAndGo
Allows access to Pack and Go.

### IPlaneManipulator
Allows access to a plane that has a manipulator.

### IPrint3DDialog
Allows access to the Print 3D dialog.

### IPropertyManagerPage2
Provides add-in applications the ability to display and use views that have the look and feel of SOLIDWORKS PropertyManager pages.

### IPropertyManagerPageActiveX
Allows you to access a PropertyManager page ActiveX control.

### IPropertyManagerPageBitmap
Allows you to access a PropertyManager page bitmap.

### IPropertyManagerPageBitmapButton
Allows you to access a PropertyManager page bitmap button control.

### IPropertyManagerPageButton
Allows you to access a PropertyManager page button control.

### IPropertyManagerPageCheckbox
Allows you to access a PropertyManager page check box.

### IPropertyManagerPageComboBox
Allows you to access a PropertyManager page combo box control.

### IPropertyManagerPageControl
Provides a set of methods and properties common to all PropertyManager page controls.

### IPropertyManagerPageGroup
Allows you to access a PropertyManager page group control. <!--kadov_tag{{}}-->

### IPropertyManagerPageLabel
Allows you to access a PropertyManager page label control.

### IPropertyManagerPageListbox
Allows you to access a PropertyManager page list box control.

### IPropertyManagerPageNumberbox
Allows you to access a PropertyManager page number box control.

### IPropertyManagerPageOption
Allows you to access to a PropertyManager page radio button control.

### IPropertyManagerPageSelectionbox
Allows you to access a PropertyManager page selection box control.

### IPropertyManagerPageSlider
Allows you to access a PropertyManager page slider control.

### IPropertyManagerPageTab
Allows you to access a PropertyManager page tab control.

### IPropertyManagerPageTextbox
Allows you to access a PropertyManager page text box control.

### IPropertyManagerPageWindowFromHandle
Allows you to access a PropertyManager page .NET control.

### IRenamedDocumentReferences
Allows you to update references to a renamed file in unopened documents.

### ISelectionSet
Allows access to a selection set.

### ISelectionSetFolder
Allows access to the Selection Sets folder.

### ISelectionSetItem
Allows access to a selection set item.

### IDimensionSensorData
Allows access to a Measurement (dimension) sensor feature.

### ISensor
Allows access to a sensor, which can monitor selected properties of parts and assemblies and alert you when the sensor's values deviate from the specified limits.

### ISnapShot
Allows access to the snapshot of the graphics area of an assembly opened in Large Design Review mode.

### IMBD3DPdfData
Gains access to the details for publishing a SOLIDWORKS MBD 3D PDF.

### ITextAndCustomProperty
Gains access to the text and custom properties in a theme of a SOLIDWORKS MBD 3D PDF.

### IStatusBarPane
Controls user-created status bar panes in the lower-right corner of the SOLIDWORKS status bar.

### ISwHtmlInterface
（外部 CHM 参考文档）

### ISWPropertySheet
Allows applications to add pages to certain property sheets that are exported by the SOLIDWORKS application.

### ITaskpaneView
Provides access to an application-level Task Pane.

### ITreeControlItem
Allows you to traverse items in the FeatureManager design tree exactly as they appear in the FeatureManager design tree.

### ITriadManipulator
Allows access to triad manipulators, which are: Similar to the SOLIDWORKS triad. Used to move and rotate assembly components, move/copy bodies, and so on.

### IUserNotificationDefinition
Allows access to a user notification of an add-in.

### IUserProgressBar
Allows access to a progress indicator that indicates how much longer a SOLIDWORKS operation will take.

## 自定义接口

### IMessageBarHandler
（外部 CHM 参考文档）

### IPropertyManagerPage2Handler9
（外部 CHM 参考文档）

### ISW3DPrinter
（外部 CHM 参考文档）

### IswCalloutHandler
（外部 CHM 参考文档）

### ISwAddin
（外部 CHM 参考文档）

### ISwAddinBroker
（外部 CHM 参考文档）

### ISwColorContour1
（外部 CHM 参考文档）

### ISwComFeature
（外部 CHM 参考文档）

### ISwManipulatorHandler2
（外部 CHM 参考文档）

### ISwPEClassFactory
Allows access to the callback object used by ISwPEManager to send a license key back to SOLIDWORKS for SOLIDWORKS Partner entitlement verification.

### ISwPEManager
（外部 CHM 参考文档）

### ISwPEToken
*For future use*

### ISwQuickTip
（外部 CHM 参考文档）

### ISwScene
Allows access to the scene of a model.

### ISwUndoAPIHandler
（外部 CHM 参考文档）

### IUserNotificationHandler
（外部 CHM 参考文档）


## 其他 API 模块

以下 API 模块在文档中可用，但未在本摘要中详细列出。它们涵盖专门的功能：

- **API_GB**：API_GB 文档
- **apihelp**：apihelp 文档
- **cworksapi**：cworksapi 文档
- **cworksapivb6**：cworksapivb6 文档
- **dsgnchkapi**：dsgnchkapi 文档
- **dsgnchkapivb6**：dsgnchkapivb6 文档
- **emodelapi**：emodelapi 文档
- **emodelapivb6**：emodelapivb6 文档
- **emodeltoolkit**：emodeltoolkit 文档
- **epdmapi**：epdmapi 文档
- **fworksapi**：fworksapi 文档
- **fworksapivb6**：fworksapivb6 文档
- **obsoleteapi**：obsoleteapi 文档
- **pdmprowebapihelp**：pdmprowebapihelp 文档
- **pdmworksapi**：pdmworksapi 文档
- **pdmworksapivb6**：pdmworksapivb6 文档
- **routingapi**：routingapi 文档
- **routingapivb6**：routingapivb6 文档
- **sustainabilityapi**：sustainabilityapi 文档
- **sustainabilityapivb6**：sustainabilityapivb6 文档
- **sw3dprinterapi**：sw3dprinterapi 文档
- **sw3dprinterapivb6**：sw3dprinterapivb6 文档
- **swcommands**：swcommands 文档
- **swconst**：swconst 文档
- **swcostingapi**：swcostingapi 文档
- **swcostingapivb6**：swcostingapivb6 文档
- **swdimxpertapi**：swdimxpertapi 文档
- **swdimxpertapivb6**：swdimxpertapivb6 文档
- **swdocmgrapi**：swdocmgrapi 文档
- **swdocmgrapivb6**：swdocmgrapivb6 文档
- **swhtmlcontrolapi**：swhtmlcontrolapi 文档
- **swhtmlcontrolapivb6**：swhtmlcontrolapivb6 文档
- **swinspectionapi**：swinspectionapi 文档
- **swinspectionapivb6**：swinspectionapivb6 文档
- **swmotionstudyapi**：swmotionstudyapi 文档
- **swmotionstudyapivb6**：swmotionstudyapivb6 文档
- **swpublishedapi**：swpublishedapi 文档
- **swpublishedapivb6**：swpublishedapivb6 文档
- **swscanto3dapi**：swscanto3dapi 文档
- **swscanto3dapivb6**：swscanto3dapivb6 文档
- **swutilitiesapi**：swutilitiesapi 文档
- **swutilitiesapivb6**：swutilitiesapivb6 文档
- **toolboxapi**：toolboxapi 文档
- **toolboxapivb6**：toolboxapivb6 文档
