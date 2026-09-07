# ZoneSplit Merge Parts By Zone Plan

## Goal

Sau khi ZoneSplit tao/cat Parts va gan `BIM_ZoneID`, `BIM_ZoneName`, can co cach gom cac Part cung zone thanh mot "khoi" de phuc vu 4D/QTO va quan ly theo zone.

## Constraint

Trong Revit, Part la doi tuong phu thuoc vao element goc va lich su chia Part. Viec "merge" nhieu Parts khac source element thanh mot Part native duy nhat khong phai workflow on dinh cho API. Vi vay khong nen coi native Part merge la huong chinh neu can chay hang loat.

## Recommended Option: Zone Aggregation DirectShape

1. Sau khi gan `BIM_ZoneID`, collect tat ca Parts theo tung zone.
2. Lay solid cua tung Part bang `SolidExtractor`.
3. Tao mot `DirectShape` moi theo category `Generic Models` cho moi zone.
4. Gan geometry gom danh sach solid cua tat ca Parts trong zone vao DirectShape.
5. Gan parameter cho DirectShape:
   - `BIM_ZoneID`
   - `BIM_ZoneName`
   - `BIM_SourcePartIds`
   - `BIM_AggregateType = ZonePartAggregate`
6. An hoac giu Parts goc tuy workflow:
   - Giu Parts goc de export/trace nguon.
   - Dung view filter de hien thi DirectShape aggregate khi can xem khoi zone.

## Alternative Option: Assembly/Group Per Zone

1. Collect Parts theo `BIM_ZoneID`.
2. Tao AssemblyInstance hoac Group cho tung zone.
3. Khong tao solid union, chi gom cac element de quan ly.

Uu diem: giu native Parts, it rui ro geometry.
Nhuoc diem: khong thanh mot khoi solid duy nhat.

## Not Recommended: Boolean Union Native Parts

Boolean union tat ca solid roi thay the Parts bang mot element moi co the dung cho view/QTO, nhung:

- De loi voi geometry phuc tap.
- Mat lien ket native Part/source element.
- Kho xu ly update khi zone hoac model thay doi.

## Proposed Implementation Steps

1. Them `ZonePartAggregationService`.
2. Them mode hoac checkbox `Create aggregate by zone`.
3. Sau `AssignParametersToSubElements`, collect Parts theo zone.
4. Tao/cap nhat DirectShape aggregate theo stable key `ZoneSplit_Aggregate_{ZoneId}`.
5. Xoa aggregate cu do add-in tao truoc khi tao moi, khong xoa Parts goc.
6. Bao cao so zone aggregate va so Parts moi zone.

## Acceptance Criteria

- Moi zone co it nhat mot Part se co mot DirectShape aggregate.
- Aggregate co dung `BIM_ZoneID` va `BIM_ZoneName`.
- Parts goc van ton tai va van giu parameter zone.
- Chay lai add-in khong tao duplicate aggregate cu.
